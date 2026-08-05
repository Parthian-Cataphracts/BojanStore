using Bojan.Api.Auth;
using Bojan.Application.Contracts;
using Bojan.Application.Administration;
using Bojan.Application.Support;
using Bojan.Domain.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Bojan.Api.Endpoints;

/// <summary>
/// The support mailbox — the address customers write to, read and answered
/// from the panel.
/// </summary>
/// <remarks>
/// <para>
/// Reads sit behind the support section, because reading and answering
/// customer mail is what a support operator does. The settings sit behind
/// <b>owner</b>: they carry the credential to a mail account, and someone
/// trusted to answer customers is not thereby trusted to point the shop's
/// support address at a server of their choosing.
/// </para>
/// <para>
/// Every handler answers a <see cref="MailResult"/> rather than throwing,
/// because every failure here belongs to the operator — a wrong host, a
/// rejected password, a server that is down — and each needs its own sentence
/// on screen instead of one 500 that says nothing.
/// </para>
/// </remarks>
public static class MailboxEndpoints
{
    public static void MapMailboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/support/mailbox").NoStore();

        group.MapGet("/conversations", ListConversations)
            .RequireAuthorization(AuthorizationPolicies.AdminSupport)
            .RequireSection(PanelSection.Support);

        group.MapGet("/conversations/{id}", GetConversation)
            .RequireAuthorization(AuthorizationPolicies.AdminSupport)
            .RequireSection(PanelSection.Support);

        group.MapGet("/attachments/{folder}/{uid:int}/{index:int}", GetAttachment)
            .RequireAuthorization(AuthorizationPolicies.AdminSupport)
            .RequireSection(PanelSection.Support);

        group.MapGet("/unread-count", GetUnreadCount)
            .RequireAuthorization(AuthorizationPolicies.AdminSupport)
            .RequireSection(PanelSection.Support);

        group.MapPost("/reply", Reply)
            .RequireAuthorization(AuthorizationPolicies.AdminSupport)
            .RequireSection(PanelSection.Support);

        // Owner only — see the remarks above.
        group.MapGet("/settings", GetSettings)
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings);

        group.MapPost("/settings", SaveSettings)
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings);

        group.MapPost("/settings/test", TestConnection)
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings);
    }

    /// <summary>
    /// Turns a mailbox result into a response.
    /// </summary>
    /// <remarks>
    /// A failure is a 502, not a 500: the thing that went wrong is the mail
    /// server this API is talking to on the operator's behalf, and the message
    /// travels with it so the screen can say which.
    /// </remarks>
    private static IResult From<T>(MailResult<T> result) =>
        result is { Ok: true, Value: { } value }
            ? Results.Ok(value)
            : Results.Problem(
                title: "mailbox-unavailable",
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway);

    private static IResult From(MailResult result) =>
        result.Ok
            ? Results.NoContent()
            : Results.Problem(
                title: "mailbox-unavailable",
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway);

    private static async Task<IResult> ListConversations(
        IMailboxService mailbox,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] bool unread = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25) =>
        From(await mailbox.ListConversationsAsync(
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100),
            q,
            unread,
            cancellationToken));

    private static async Task<IResult> GetConversation(
        string id, IMailboxService mailbox, CancellationToken cancellationToken) =>
        From(await mailbox.GetConversationAsync(id, cancellationToken));

    /// <summary>
    /// Streams one attachment back.
    /// </summary>
    /// <remarks>
    /// <c>Content-Disposition: attachment</c> without exception, and the file
    /// name is the sender's text — so it is served as a download rather than
    /// rendered. An inline HTML or SVG attachment displayed on the panel's own
    /// origin would be script execution with the operator's session, which is
    /// the whole thing the sanitizer exists to prevent one layer up.
    /// </remarks>
    private static async Task<IResult> GetAttachment(
        string folder,
        int uid,
        int index,
        IMailboxService mailbox,
        CancellationToken cancellationToken)
    {
        // Both checked here rather than left to the service, so a malformed
        // link is refused without opening an IMAP connection to discover it.
        // The `:int` route constraint accepts a negative, and a UID is
        // unsigned.
        if (uid < 0)
        {
            return ApiResults.Problem(Application.Common.UseCaseError.Invalid, "uid");
        }

        if (index < 0)
        {
            return ApiResults.Problem(Application.Common.UseCaseError.Invalid, "index");
        }

        var result = await mailbox.GetAttachmentAsync(folder, (uint)uid, index, cancellationToken);
        if (result is not { Ok: true, Value: { } attachment })
        {
            return Results.Problem(
                title: "mailbox-unavailable",
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.File(
            attachment.Content,
            // Never the declared type: it is chosen by whoever sent the mail,
            // and honouring it is how an "image" gets rendered as HTML.
            "application/octet-stream",
            attachment.FileName);
    }

    private static async Task<IResult> GetUnreadCount(IMailboxService mailbox, CancellationToken cancellationToken) =>
        Results.Ok(new { count = await mailbox.GetUnreadCountAsync(cancellationToken) });

    /// <summary>
    /// Sends the reply through <see cref="AdminOperationsService"/> rather than
    /// straight to the mailbox, so the audit entry is written where every other
    /// operator action's is.
    /// </summary>
    private static async Task<IResult> Reply(
        MailReplyBody body,
        AdminOperationsService operations,
        CancellationToken cancellationToken) =>
        From(await operations.ReplyToMailAsync(
            new MailSendRequest(
                To: body.To ?? [],
                Cc: body.Cc ?? [],
                Subject: body.Subject ?? string.Empty,
                Body: body.Body ?? string.Empty,
                ReplyToFolder: body.ReplyToFolder,
                InReplyToUid: body.InReplyToUid),
            cancellationToken));

    private static async Task<IResult> GetSettings(IMailboxSettingsStore store, CancellationToken cancellationToken) =>
        Results.Ok(await store.GetAsync(cancellationToken));

    private static async Task<IResult> SaveSettings(
        MailboxSettingsBody body,
        IMailboxSettingsStore store,
        CancellationToken cancellationToken)
    {
        await store.SaveAsync(
            new MailboxSettingsDto(
                Enabled: body.Enabled,
                ImapHost: body.ImapHost ?? string.Empty,
                ImapPort: body.ImapPort,
                ImapUseSsl: body.ImapUseSsl,
                SmtpHost: body.SmtpHost ?? string.Empty,
                SmtpPort: body.SmtpPort,
                SmtpUseSsl: body.SmtpUseSsl,
                Username: body.Username ?? string.Empty,
                HasPassword: false,
                Address: body.Address ?? string.Empty,
                DisplayName: body.DisplayName ?? string.Empty),
            // Absent means "keep what is stored" — the form never shows the
            // password, so an empty field cannot mean "clear it".
            body.Password,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> TestConnection(IMailboxService mailbox, CancellationToken cancellationToken) =>
        From(await mailbox.TestConnectionAsync(cancellationToken));
}

/// <summary>A reply composed on the thread screen.</summary>
public sealed record MailReplyBody(
    IReadOnlyList<string>? To,
    IReadOnlyList<string>? Cc,
    string? Subject,
    string? Body,
    string? ReplyToFolder,
    uint? InReplyToUid);

/// <summary>The mailbox settings form.</summary>
public sealed record MailboxSettingsBody(
    bool Enabled,
    string? ImapHost,
    int ImapPort,
    bool ImapUseSsl,
    string? SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string? Username,
    /// <summary>Null to keep the stored one; empty to clear it.</summary>
    string? Password,
    string? Address,
    string? DisplayName);
