using Bojan.Application.Contracts;

namespace Bojan.Application.Support;

/// <summary>
/// The shop's own mailbox — the address customers write to.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not <see cref="Auth.IEmailSender"/>. That one is the
/// transactional path every order and password reset depends on: one-way, no
/// account, and breaking it stops mail reaching customers. This is a second and
/// independent account that only the panel's inbox uses, so an operator can
/// point it somewhere else — or get it wrong — without touching delivery.
/// </para>
/// <para>
/// Replies go out through this mailbox's own SMTP rather than the transactional
/// sender, so the customer sees the address they wrote to as the sender and
/// their mail client keeps the exchange on one thread.
/// </para>
/// </remarks>
public interface IMailboxService
{
    /// <summary>
    /// The inbox as conversations — INBOX and Sent, grouped into topics.
    /// </summary>
    /// <remarks>
    /// Grouped rather than listed flat because a support exchange is a
    /// back-and-forth, and a flat list interleaves four unrelated topics from
    /// one customer with the replies to each.
    /// </remarks>
    Task<MailResult<MailConversationPageDto>> ListConversationsAsync(
        int page,
        int pageSize,
        string? search,
        bool unreadOnly,
        CancellationToken cancellationToken);

    /// <summary>One whole thread. Opening it marks the customer's messages read.</summary>
    Task<MailResult<MailConversationDetailDto>> GetConversationAsync(string id, CancellationToken cancellationToken);

    Task<MailResult<MailAttachmentContentDto>> GetAttachmentAsync(
        string folder,
        uint uid,
        int index,
        CancellationToken cancellationToken);

    Task<MailResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Unread count for the nav badge.
    /// </summary>
    /// <remarks>
    /// Zero rather than an error when the mailbox is off or unreachable. It is
    /// rendered beside every screen in the panel, and a badge is not worth
    /// failing a page over.
    /// </remarks>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>Does this configuration actually work — the settings screen's test button.</summary>
    Task<MailResult> TestConnectionAsync(CancellationToken cancellationToken);
}

/// <summary>Reads and writes the mailbox connection settings.</summary>
public interface IMailboxSettingsStore
{
    /// <summary>What the panel may see — never the password.</summary>
    Task<MailboxSettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="password">
    /// Null leaves the stored one alone, which is what a form that never shows
    /// the password has to mean when it is submitted with the field empty.
    /// </param>
    Task SaveAsync(MailboxSettingsDto settings, string? password, CancellationToken cancellationToken);
}
