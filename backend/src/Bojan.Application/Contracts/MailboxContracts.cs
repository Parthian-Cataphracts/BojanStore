namespace Bojan.Application.Contracts;

/// <summary>One participant on a message.</summary>
public sealed record MailAddressDto(string Name, string Address);

/// <summary>A file that arrived attached to a message.</summary>
public sealed record MailAttachmentDto(int Index, string FileName, string ContentType, long Size);

/// <summary>
/// One message inside a thread, carrying its whole body.
/// </summary>
/// <remarks>
/// The thread renders at once rather than a message at a time, which is what an
/// operator reading a support conversation actually wants — the alternative is
/// a list of subjects they have to open one by one to reconstruct what was
/// said.
/// </remarks>
public sealed record MailThreadMessageDto(
    string Folder,
    uint Uid,
    /// <summary>True for something the customer sent, false for one of our replies.</summary>
    bool FromCustomer,
    MailAddressDto From,
    IReadOnlyList<MailAddressDto> To,
    DateTimeOffset Date,
    string TextBody,
    /// <summary>Already sanitized; still rendered inside a sandboxed frame.</summary>
    string HtmlBody,
    /// <summary>Sanitizing removed remote images or frames, so the screen can say so.</summary>
    bool HadRemoteContent,
    bool Seen,
    IReadOnlyList<MailAttachmentDto> Attachments);

/// <summary>One row of the inbox list.</summary>
public sealed record MailConversationSummaryDto(
    string Id,
    string Subject,
    /// <summary>The outside participant — the customer, never the support address.</summary>
    MailAddressDto Party,
    DateTimeOffset LastDate,
    int Count,
    int Unread,
    string Preview,
    bool HasAttachments,
    /// <summary>False when the last message was ours, which reads as "waiting on them".</summary>
    bool LastFromCustomer);

public sealed record MailConversationPageDto(
    IReadOnlyList<MailConversationSummaryDto> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>One whole conversation.</summary>
public sealed record MailConversationDetailDto(
    string Id,
    string Subject,
    MailAddressDto Party,
    /// <summary>Where the newest inbound message lives, so a reply threads onto it.</summary>
    string? ReplyFolder,
    uint? ReplyUid,
    IReadOnlyList<MailThreadMessageDto> Messages);

/// <summary>A reply composed in the panel.</summary>
public sealed record MailSendRequest(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    /// <summary>Set when replying, so the original's Message-Id can be threaded onto.</summary>
    string? ReplyToFolder = null,
    uint? InReplyToUid = null);

public sealed record MailAttachmentContentDto(byte[] Content, string ContentType, string FileName);

/// <summary>
/// What the panel may see of the mailbox configuration.
/// </summary>
/// <remarks>
/// No password. It is write-only from the panel's point of view: it goes in
/// when set and never comes back out, so a settings screen left open on a
/// shared machine is not a credential on display. <see cref="HasPassword"/> is
/// how the form knows to show "saved" instead of an empty box.
/// </remarks>
public sealed record MailboxSettingsDto(
    bool Enabled,
    string ImapHost,
    int ImapPort,
    bool ImapUseSsl,
    string SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string Username,
    bool HasPassword,
    string Address,
    string DisplayName);

/// <summary>
/// The outcome of a mailbox operation.
/// </summary>
/// <remarks>
/// A result rather than an exception because every failure here is the
/// operator's to fix — a wrong host, a rejected password, a server that is
/// down, a folder that is gone — and each one needs its own sentence on the
/// screen. Thrown, they would all collapse into one 500 that says nothing.
/// </remarks>
public sealed record MailResult(bool Ok, string? Error = null)
{
    public static readonly MailResult Success = new(true);

    public static MailResult Fail(string error) => new(false, error);
}

public sealed record MailResult<T>(bool Ok, T? Value, string? Error = null)
{
    public static MailResult<T> Success(T value) => new(true, value);

    public static MailResult<T> Fail(string error) => new(false, default, error);
}
