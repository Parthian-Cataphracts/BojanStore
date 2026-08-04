using Bojan.Application.Common;
using Bojan.Application.Support;

namespace Bojan.Api.Endpoints;

/// <summary>
/// The storefront widget's half of live chat — no credential, keyed by the
/// opaque visitor id the widget mints and keeps client-side. The panel's half
/// lives in <c>AdminReadEndpoints</c> / <c>AdminWriteEndpoints</c> under
/// <c>/admin/chat</c>, behind the same <c>AdminSupport</c> gate as the
/// ticket threads it sits beside.
/// </summary>
public static class LiveChatEndpoints
{
    public static void MapLiveChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/chat").AllowAnonymous();

        group.MapGet("/{visitorId:guid}", GetConversation);
        group.MapPost("/{visitorId:guid}/messages", SendMessage).RequireRateLimiting(RateLimitPolicies.PublicWrite);
    }

    private static async Task<IResult> GetConversation(
        Guid visitorId, LiveChatService chat, CancellationToken cancellationToken) =>
        Results.Ok(await chat.GetConversationAsVisitorAsync(visitorId, cancellationToken));

    private static async Task<IResult> SendMessage(
        Guid visitorId,
        LiveChatMessageRequest body,
        LiveChatService chat,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var text = body.Body?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 4000)
        {
            return Results.BadRequest();
        }

        await chat.SendVisitorMessageAsync(visitorId, currentUser.CustomerId, text, cancellationToken);
        return Results.Ok();
    }
}

public sealed record LiveChatMessageRequest(string? Body);
