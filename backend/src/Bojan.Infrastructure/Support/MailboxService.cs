using System.Text;
using Bojan.Application.Contracts;
using Bojan.Application.Support;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Bojan.Infrastructure.Support;

/// <summary>
/// The support mailbox, over IMAP and SMTP.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is stored locally. The mail server is the record — this reads it on
/// demand and writes replies back into it, so the panel is one more client of
/// the same mailbox rather than a copy that can fall out of step with what an
/// operator sees in their own mail app.
/// </para>
/// <para>
/// UIDs, never sequence numbers: a sequence number shifts the moment anything
/// else touches the folder, so an action built on one eventually acts on the
/// wrong message.
/// </para>
/// </remarks>
public sealed class MailboxService(
    MailboxSettingsStore settings,
    IMemoryCache cache,
    ILogger<MailboxService> logger) : IMailboxService
{
    /// <summary>How many recent messages per folder are read when building threads.</summary>
    /// <remarks>
    /// A support mailbox is a working queue, not an archive: what matters is
    /// the recent traffic, and scanning the whole folder to show the first page
    /// would make the screen slower the longer the shop had been open.
    /// </remarks>
    private const int MaxScan = 400;

    /// <summary>
    /// How long a scan is reused.
    /// </summary>
    /// <remarks>
    /// The threading scan reads several hundred message headers from two
    /// folders, and the list, the search and the paging all need the same one.
    /// Without this, opening the inbox and clicking to page two re-read the
    /// whole mailbox twice. Short enough that a mail arriving is visible almost
    /// at once, and any action that changes state clears it outright.
    /// </remarks>
    private static readonly TimeSpan ScanCacheLifetime = TimeSpan.FromSeconds(20);

    private const string ScanCacheKey = "mailbox:scan";

    // -- connection ----------------------------------------------------------

    private async Task<(MailboxSettingsDto Settings, string Password)?> ConfiguredAsync(CancellationToken cancellationToken)
    {
        var (configured, password) = await settings.GetWithPasswordAsync(cancellationToken);

        if (!configured.Enabled
            || string.IsNullOrWhiteSpace(configured.ImapHost)
            || string.IsNullOrWhiteSpace(configured.Username))
        {
            return null;
        }

        return (configured, password);
    }

    private async Task<MailResult<T>> WithImapAsync<T>(
        Func<ImapClient, CancellationToken, Task<MailResult<T>>> work,
        CancellationToken cancellationToken)
    {
        var configured = await ConfiguredAsync(cancellationToken);
        if (configured is null)
        {
            return MailResult<T>.Fail("صندوق ورودی پیکربندی یا فعال نشده است. از «تنظیمات صندوق» اطلاعات IMAP را وارد کنید.");
        }

        var (mailbox, password) = configured.Value;

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(
                mailbox.ImapHost,
                mailbox.ImapPort,
                mailbox.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            await client.AuthenticateAsync(mailbox.Username, password, cancellationToken);

            var result = await work(client, cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationException)
        {
            // Called out on its own because it is the most common setup mistake
            // and the one failure where the operator knows exactly what to do.
            return MailResult<T>.Fail("نام کاربری یا گذرواژه صندوق پذیرفته نشد.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "IMAP operation failed against {Host}:{Port}", mailbox.ImapHost, mailbox.ImapPort);
            return MailResult<T>.Fail($"اتصال به سرور ایمیل ممکن نشد: {exception.Message}");
        }
    }

    private static async Task<IMailFolder?> OpenAsync(
        ImapClient client,
        string name,
        FolderAccess access,
        CancellationToken cancellationToken)
    {
        try
        {
            var folder = await client.GetFolderAsync(name, cancellationToken);
            await folder.OpenAsync(access, cancellationToken);
            return folder;
        }
        catch (FolderNotFoundException)
        {
            // A server that names its Sent folder something else is a
            // configuration difference, not a failure: the thread is then built
            // from the inbound half alone.
            return null;
        }
    }

    /// <summary>
    /// Finds the folder the server keeps sent mail in.
    /// </summary>
    /// <remarks>
    /// By its special-use flag first, because that is how a server declares
    /// what a folder is for; the names are a fallback, since they are localised
    /// and differ between servers.
    /// </remarks>
    private static async Task<IMailFolder?> SentFolderAsync(ImapClient client, CancellationToken cancellationToken)
    {
        try
        {
            if (client.GetFolder(SpecialFolder.Sent) is { } special)
            {
                return special;
            }
        }
        catch (NotSupportedException)
        {
            // The server does not advertise special-use folders at all.
        }

        foreach (var name in new[] { "Sent", "INBOX.Sent", "Sent Items", "Sent Messages" })
        {
            try
            {
                return await client.GetFolderAsync(name, cancellationToken);
            }
            catch (FolderNotFoundException)
            {
                // Try the next spelling.
            }
        }

        return null;
    }

    // -- threading -----------------------------------------------------------

    /// <summary>The two headers a reply needs to thread onto the original.</summary>
    private sealed record ThreadingHeaders(string MessageId, string References);

    private sealed record ThreadRow(
        string Folder,
        uint Uid,
        bool FromCustomer,
        MailAddressDto Party,
        string RawSubject,
        string Subject,
        DateTimeOffset Date,
        string Preview,
        bool Seen,
        bool HasAttachments);

    private async Task<MailResult<List<ThreadRow>>> ScanAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ScanCacheKey, out List<ThreadRow>? cached) && cached is not null)
        {
            return MailResult<List<ThreadRow>>.Success(cached);
        }

        var result = await WithImapAsync(async (client, token) =>
        {
            var rows = new List<ThreadRow>();

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, token);
            rows.AddRange(await ReadAsync(inbox, fromCustomer: true, token));
            await inbox.CloseAsync(false, token);

            if (await SentFolderAsync(client, token) is { } sent)
            {
                await sent.OpenAsync(FolderAccess.ReadOnly, token);
                rows.AddRange(await ReadAsync(sent, fromCustomer: false, token));
                await sent.CloseAsync(false, token);
            }

            return MailResult<List<ThreadRow>>.Success(rows);
        }, cancellationToken);

        if (result.Ok && result.Value is not null)
        {
            cache.Set(ScanCacheKey, result.Value, ScanCacheLifetime);
        }

        return result;
    }

    /// <summary>Drops the cached scan, so the next read sees what just changed.</summary>
    private void InvalidateScan() => cache.Remove(ScanCacheKey);

    private static async Task<List<ThreadRow>> ReadAsync(
        IMailFolder folder,
        bool fromCustomer,
        CancellationToken cancellationToken)
    {
        var rows = new List<ThreadRow>();

        var uids = await folder.SearchAsync(SearchQuery.All, cancellationToken);
        var recent = uids.OrderByDescending(uid => uid.Id).Take(MaxScan).ToList();
        if (recent.Count == 0)
        {
            return rows;
        }

        var summaries = await folder.FetchAsync(
            recent,
            MessageSummaryItems.UniqueId
            | MessageSummaryItems.Envelope
            | MessageSummaryItems.Flags
            | MessageSummaryItems.BodyStructure
            | MessageSummaryItems.PreviewText,
            cancellationToken);

        foreach (var summary in summaries)
        {
            var envelope = summary.Envelope;
            var flags = summary.Flags ?? MessageFlags.None;

            // The outside party is whoever is not us: the sender of something
            // that came in, the recipient of something we sent. That is what
            // makes both halves of an exchange group together.
            var party = fromCustomer ? FirstAddress(envelope?.From) : FirstAddress(envelope?.To);
            var rawSubject = Clean(envelope?.Subject);

            rows.Add(new ThreadRow(
                Folder: folder.FullName,
                Uid: summary.UniqueId.Id,
                FromCustomer: fromCustomer,
                Party: party,
                RawSubject: rawSubject,
                Subject: MailSubject.Normalize(rawSubject),
                Date: envelope?.Date ?? summary.InternalDate ?? DateTimeOffset.MinValue,
                Preview: Truncate(Clean(summary.PreviewText), 200),
                Seen: flags.HasFlag(MessageFlags.Seen),
                HasAttachments: summary.Attachments?.Any() == true));
        }

        return rows;
    }

    /// <summary>
    /// The group key and the public id in one.
    /// </summary>
    /// <remarks>
    /// The outside party's address plus the normalised subject, encoded so it
    /// survives in a URL. Derived rather than stored, so opening a conversation
    /// re-derives the same id from a fresh scan without any server-side state
    /// to keep in step.
    /// </remarks>
    private static string ConversationId(ThreadRow row)
    {
        var key = $"{row.Party.Address.ToLowerInvariant()} {row.Subject.ToLowerInvariant()}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static List<MailConversationSummaryDto> Group(IEnumerable<ThreadRow> rows) =>
    [
        .. rows
            .Where(row => !string.IsNullOrEmpty(row.Party.Address))
            .GroupBy(ConversationId)
            .Select(group =>
            {
                var ordered = group.OrderBy(row => row.Date).ToList();
                var last = ordered[^1];

                return new MailConversationSummaryDto(
                    Id: group.Key,
                    // Taken from the newest message, so a renamed thread shows
                    // its latest form.
                    Subject: last.RawSubject.Length > 0 ? last.RawSubject : "(بدون موضوع)",
                    Party: ordered.FirstOrDefault(row => row.FromCustomer)?.Party ?? last.Party,
                    LastDate: last.Date,
                    Count: ordered.Count,
                    // Only inbound messages can be unread — our own replies are
                    // not something anyone has to read.
                    Unread: ordered.Count(row => row.FromCustomer && !row.Seen),
                    Preview: last.Preview,
                    HasAttachments: ordered.Any(row => row.HasAttachments),
                    LastFromCustomer: last.FromCustomer);
            }),
    ];

    // -- reads ---------------------------------------------------------------

    public async Task<MailResult<MailConversationPageDto>> ListConversationsAsync(
        int page,
        int pageSize,
        string? search,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var scan = await ScanAsync(cancellationToken);
        if (!scan.Ok || scan.Value is null)
        {
            return MailResult<MailConversationPageDto>.Fail(scan.Error ?? "خواندن صندوق ممکن نشد.");
        }

        var conversations = Group(scan.Value)
            .OrderByDescending(conversation => conversation.LastDate)
            .ToList();

        if (unreadOnly)
        {
            conversations = conversations.Where(conversation => conversation.Unread > 0).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Filtered here rather than through IMAP SEARCH: the scan is
            // already in hand and cached, and a server-side search would need a
            // round trip per keystroke while matching only what the server
            // indexes.
            var needle = search.Trim();
            conversations = conversations
                .Where(conversation =>
                    conversation.Subject.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || conversation.Party.Address.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || conversation.Party.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || conversation.Preview.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = conversations.Count;
        var items = conversations.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return MailResult<MailConversationPageDto>.Success(
            new MailConversationPageDto(items, total, page, pageSize));
    }

    public async Task<MailResult<MailConversationDetailDto>> GetConversationAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var scan = await ScanAsync(cancellationToken);
        if (!scan.Ok || scan.Value is null)
        {
            return MailResult<MailConversationDetailDto>.Fail(scan.Error ?? "خواندن صندوق ممکن نشد.");
        }

        var rows = scan.Value.Where(row => ConversationId(row) == id).OrderBy(row => row.Date).ToList();
        if (rows.Count == 0)
        {
            return MailResult<MailConversationDetailDto>.Fail("این گفتگو پیدا نشد.");
        }

        var result = await WithImapAsync(async (client, token) =>
        {
            var messages = new List<MailThreadMessageDto>();
            var opened = new Dictionary<string, IMailFolder>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (!opened.TryGetValue(row.Folder, out var folder))
                {
                    // Read-write, because opening a conversation marks the
                    // customer's messages read — the way opening one in a mail
                    // client does.
                    var open = await OpenAsync(client, row.Folder, FolderAccess.ReadWrite, token);
                    if (open is null)
                    {
                        continue;
                    }

                    opened[row.Folder] = open;
                    folder = open;
                }

                var uid = new UniqueId(row.Uid);
                var message = await folder.GetMessageAsync(uid, token);
                var (html, hadRemote) = MailHtmlSanitizer.Sanitize(message.HtmlBody);

                if (row.FromCustomer && !row.Seen)
                {
                    await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, token);
                }

                messages.Add(new MailThreadMessageDto(
                    Folder: row.Folder,
                    Uid: row.Uid,
                    FromCustomer: row.FromCustomer,
                    From: FirstAddress(message.From),
                    To: Addresses(message.To),
                    Date: message.Date,
                    TextBody: Clean(message.TextBody),
                    HtmlBody: html,
                    HadRemoteContent: hadRemote,
                    Seen: true,
                    Attachments: Attachments(message)));
            }

            foreach (var folder in opened.Values)
            {
                await folder.CloseAsync(false, token);
            }

            var newestInbound = rows.LastOrDefault(row => row.FromCustomer);
            var last = rows[^1];

            return MailResult<MailConversationDetailDto>.Success(new MailConversationDetailDto(
                Id: id,
                Subject: last.RawSubject.Length > 0 ? last.RawSubject : "(بدون موضوع)",
                Party: rows.FirstOrDefault(row => row.FromCustomer)?.Party ?? last.Party,
                ReplyFolder: newestInbound?.Folder,
                ReplyUid: newestInbound?.Uid,
                Messages: messages));
        }, cancellationToken);

        // Flags changed, so the cached scan's read state is now stale.
        InvalidateScan();
        return result;
    }

    public Task<MailResult<MailAttachmentContentDto>> GetAttachmentAsync(
        string folder,
        uint uid,
        int index,
        CancellationToken cancellationToken) =>
        WithImapAsync(async (client, token) =>
        {
            var box = await OpenAsync(client, folder, FolderAccess.ReadOnly, token);
            if (box is null)
            {
                return MailResult<MailAttachmentContentDto>.Fail("این پوشه پیدا نشد.");
            }

            var message = await box.GetMessageAsync(new UniqueId(uid), token);
            var parts = message.Attachments.OfType<MimePart>().ToList();

            if (index < 0 || index >= parts.Count)
            {
                return MailResult<MailAttachmentContentDto>.Fail("این پیوست پیدا نشد.");
            }

            var part = parts[index];

            // A MIME part can legitimately carry no content — a truncated or
            // malformed message. Answering "not found" is honest; dereferencing
            // it would be a 500.
            if (part.Content is null)
            {
                return MailResult<MailAttachmentContentDto>.Fail("این پیوست محتوایی ندارد.");
            }

            using var buffer = new MemoryStream();
            await part.Content.DecodeToAsync(buffer, token);
            await box.CloseAsync(false, token);

            return MailResult<MailAttachmentContentDto>.Success(new MailAttachmentContentDto(
                buffer.ToArray(),
                part.ContentType?.MimeType ?? "application/octet-stream",
                SafeFileName(part.FileName, index)));
        }, cancellationToken);

    // -- sending -------------------------------------------------------------

    public async Task<MailResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken)
    {
        var configured = await ConfiguredAsync(cancellationToken);
        if (configured is null)
        {
            return MailResult.Fail("صندوق ورودی پیکربندی یا فعال نشده است.");
        }

        var (mailbox, password) = configured.Value;

        if (string.IsNullOrWhiteSpace(mailbox.SmtpHost) || string.IsNullOrWhiteSpace(mailbox.Address))
        {
            return MailResult.Fail("برای ارسال پاسخ، میزبان SMTP و آدرس فرستنده باید تنظیم شده باشند.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mailbox.DisplayName, mailbox.Address));

        if (!AddRecipients(message.To, request.To, out var badRecipient))
        {
            return MailResult.Fail($"نشانی گیرنده معتبر نیست: {badRecipient}");
        }

        if (message.To.Count == 0)
        {
            return MailResult.Fail("دست‌کم یک گیرنده لازم است.");
        }

        if (!AddRecipients(message.Cc, request.Cc, out badRecipient))
        {
            return MailResult.Fail($"نشانی رونوشت معتبر نیست: {badRecipient}");
        }

        message.Subject = request.Subject.Trim();

        // Plain text only. A reply composed in a textarea has no formatting to
        // preserve, and sending HTML would mean generating markup that then has
        // to be trusted on the way back in when it lands in Sent and is read as
        // part of the thread.
        message.Body = new TextPart("plain") { Text = request.Body };

        if (request.ReplyToFolder is { Length: > 0 } replyFolder && request.InReplyToUid is { } replyUid)
        {
            // Threading headers taken from the original, so the customer's mail
            // client keeps the exchange together instead of showing a new
            // conversation for every answer.
            var headers = await WithImapAsync(async (client, token) =>
            {
                var box = await OpenAsync(client, replyFolder, FolderAccess.ReadOnly, token);
                if (box is null)
                {
                    return MailResult<ThreadingHeaders>.Fail("پیام اصلی پیدا نشد.");
                }

                var original = await box.GetMessageAsync(new UniqueId(replyUid), token);
                await box.CloseAsync(false, token);

                return MailResult<ThreadingHeaders>.Success(new ThreadingHeaders(
                    original.MessageId ?? string.Empty,
                    string.Join(' ', original.References)));
            }, cancellationToken);

            // A reply whose original cannot be read still goes out — it simply
            // starts a new thread in the customer's client, which is better
            // than refusing to answer them.
            if (headers is { Ok: true, Value: { } value } && !string.IsNullOrEmpty(value.MessageId))
            {
                message.InReplyTo = value.MessageId;
                foreach (var reference in value.References.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.References.Add(reference);
                }

                message.References.Add(value.MessageId);
            }
        }

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                mailbox.SmtpHost,
                mailbox.SmtpPort,
                mailbox.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            await smtp.AuthenticateAsync(mailbox.Username, password, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);
        }
        catch (AuthenticationException)
        {
            return MailResult.Fail("نام کاربری یا گذرواژه برای ارسال پذیرفته نشد.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SMTP send failed against {Host}:{Port}", mailbox.SmtpHost, mailbox.SmtpPort);
            return MailResult.Fail($"ارسال پاسخ ممکن نشد: {exception.Message}");
        }

        await AppendToSentAsync(message, cancellationToken);
        InvalidateScan();
        return MailResult.Success;
    }

    /// <summary>
    /// Files the reply in the Sent folder.
    /// </summary>
    /// <remarks>
    /// SMTP delivers; it does not file. Without this the reply reaches the
    /// customer and vanishes from the shop's own record — the thread would show
    /// the question and no answer, and the operator would have no way to tell
    /// an unanswered message from an answered one.
    ///
    /// Failing to file is not failing to send. The mail is already gone, so a
    /// failure here is logged and swallowed rather than reported as a send
    /// error the operator might retry.
    /// </remarks>
    private async Task AppendToSentAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await WithImapAsync(async (client, token) =>
            {
                if (await SentFolderAsync(client, token) is { } sent)
                {
                    await sent.AppendAsync(message, MessageFlags.Seen, token);
                }

                return MailResult<bool>.Success(true);
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The reply was sent but could not be filed in the Sent folder");
        }
    }

    // -- badge and test ------------------------------------------------------

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await WithImapAsync(async (client, token) =>
            {
                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly, token);
                var unseen = await inbox.SearchAsync(SearchQuery.NotSeen, token);
                await inbox.CloseAsync(false, token);
                return MailResult<int>.Success(unseen.Count);
            }, cancellationToken);

            return result is { Ok: true, Value: var count } ? count : 0;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unread mail count could not be read");
            return 0;
        }
    }

    public async Task<MailResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var imap = await WithImapAsync(async (client, token) =>
        {
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, token);
            var count = inbox.Count;
            await inbox.CloseAsync(false, token);
            return MailResult<int>.Success(count);
        }, cancellationToken);

        if (!imap.Ok)
        {
            return MailResult.Fail(imap.Error ?? "اتصال IMAP برقرار نشد.");
        }

        var configured = await ConfiguredAsync(cancellationToken);
        if (configured is null)
        {
            return MailResult.Fail("صندوق پیکربندی نشده است.");
        }

        var (mailbox, password) = configured.Value;
        if (string.IsNullOrWhiteSpace(mailbox.SmtpHost))
        {
            // IMAP works, so reading is fine — but a support inbox nobody can
            // answer from is half a feature, and the operator should hear that
            // now rather than the first time they try to reply.
            return MailResult.Fail("اتصال دریافت (IMAP) برقرار شد، اما میزبان SMTP برای ارسال تنظیم نشده است.");
        }

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                mailbox.SmtpHost,
                mailbox.SmtpPort,
                mailbox.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            await smtp.AuthenticateAsync(mailbox.Username, password, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);
        }
        catch (AuthenticationException)
        {
            return MailResult.Fail("اتصال دریافت برقرار شد، اما گذرواژه برای ارسال (SMTP) پذیرفته نشد.");
        }
        catch (Exception exception)
        {
            return MailResult.Fail($"اتصال دریافت برقرار شد، اما ارسال (SMTP) ممکن نشد: {exception.Message}");
        }

        return MailResult.Success;
    }

    // -- helpers -------------------------------------------------------------

    private static bool AddRecipients(InternetAddressList list, IReadOnlyList<string>? input, out string invalid)
    {
        invalid = string.Empty;

        foreach (var entry in input ?? [])
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!MailboxAddress.TryParse(trimmed, out var address))
            {
                invalid = trimmed;
                return false;
            }

            list.Add(address);
        }

        return true;
    }

    private static MailAddressDto FirstAddress(InternetAddressList? list)
    {
        var mailbox = list?.Mailboxes.FirstOrDefault();
        return mailbox is null
            ? new MailAddressDto(string.Empty, string.Empty)
            : new MailAddressDto(Clean(mailbox.Name), mailbox.Address);
    }

    private static IReadOnlyList<MailAddressDto> Addresses(InternetAddressList? list) =>
        [.. (list?.Mailboxes ?? []).Select(mailbox => new MailAddressDto(Clean(mailbox.Name), mailbox.Address))];

    private static List<MailAttachmentDto> Attachments(MimeMessage message) =>
        [.. message.Attachments.OfType<MimePart>().Select((part, index) => new MailAttachmentDto(
            index,
            SafeFileName(part.FileName, index),
            part.ContentType?.MimeType ?? "application/octet-stream",
            0))];

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    /// <summary>
    /// A file name safe to put in a download header.
    /// </summary>
    /// <remarks>
    /// The name comes from the message, so it is the sender's text: a path
    /// separator in it would be a traversal attempt against whatever saves the
    /// file, and a control character can hide the real extension from the
    /// person about to open it.
    /// </remarks>
    private static string SafeFileName(string? name, int index)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"attachment-{index + 1}";
        }

        var cleaned = new string([.. name
            .Where(character => !char.IsControl(character) && character is not ('/' or '\\' or ':' or '"'))]);

        cleaned = cleaned.Trim().Trim('.');
        return cleaned.Length == 0 ? $"attachment-{index + 1}" : Truncate(cleaned, 120);
    }
}
