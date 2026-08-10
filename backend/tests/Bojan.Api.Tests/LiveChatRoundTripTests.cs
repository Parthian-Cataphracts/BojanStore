using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// The whole live chat, end to end: a visitor writes in, the conversation
/// reaches the panel's queue, an operator opens it and answers, and the answer
/// comes back to the visitor.
/// </summary>
/// <remarks>
/// Written because the two halves were only ever tested apart. Each end was
/// reachable on its own and the pair still did not meet.
/// </remarks>
public sealed class LiveChatRoundTripTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _visitor = null!;
    private HttpClient _operator = null!;
    private readonly Guid _visitorId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
        });

        _visitor = _factory.CreateClient();
        _operator = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _visitor?.Dispose();
        _operator?.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> VisitorSaysAsync(string body) =>
        _visitor.PostAsJsonAsync($"/api/chat/{_visitorId}/messages", new { body });

    [Fact]
    public async Task A_visitor_message_reaches_the_panel_and_the_reply_comes_back()
    {
        (await VisitorSaysAsync("سلام، سفارشم کی می‌رسد؟")).EnsureSuccessStatusCode();

        // The queue the operator actually opens.
        var queueResponse = await _operator.GetAsync("/api/admin/chat/conversations");
        queueResponse.EnsureSuccessStatusCode();
        var queue = await queueResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, queue.GetArrayLength());
        // Case-insensitively: the id is rendered by the database, and SQLite
        // spells a GUID in upper case where PostgreSQL uses lower. The route
        // that consumes it parses either.
        Assert.Equal(
            _visitorId.ToString(),
            queue[0].GetProperty("visitorId").GetString(),
            ignoreCase: true);
        Assert.Equal(1, queue[0].GetProperty("unreadCount").GetInt32());

        // Opening the thread marks the visitor's message read, which is what
        // the widget draws its second tick from.
        var thread = await (await _operator.GetAsync($"/api/admin/chat/conversations/{_visitorId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, thread.GetArrayLength());
        Assert.True(thread[0].GetProperty("read").GetBoolean());

        var reply = await _operator.PostAsJsonAsync(
            $"/api/admin/chat/conversations/{_visitorId}/reply",
            new { body = "سلام! تا دو روز کاری ارسال می‌شود." });
        reply.EnsureSuccessStatusCode();

        // ...and the visitor sees it, still unread until they open the panel.
        var asVisitor = await (await _visitor.GetAsync($"/api/chat/{_visitorId}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, asVisitor.GetArrayLength());
        Assert.True(asVisitor[1].GetProperty("fromSupport").GetBoolean());
        Assert.Equal("سلام! تا دو روز کاری ارسال می‌شود.", asVisitor[1].GetProperty("body").GetString());
        Assert.False(asVisitor[1].GetProperty("read").GetBoolean());

        // Reading is the visitor's own act — see LiveChatService.
        (await _visitor.PostAsync($"/api/chat/{_visitorId}/read", null)).EnsureSuccessStatusCode();

        var afterRead = await (await _visitor.GetAsync($"/api/chat/{_visitorId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterRead[1].GetProperty("read").GetBoolean());
    }

    /// <summary>A thread nobody has written into is empty, not an error.</summary>
    [Fact]
    public async Task An_unknown_visitor_has_an_empty_conversation()
    {
        var response = await _operator.GetAsync($"/api/admin/chat/conversations/{Guid.NewGuid()}");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task Replying_needs_an_operator()
    {
        await VisitorSaysAsync("سلام");

        var response = await _visitor.PostAsJsonAsync(
            $"/api/admin/chat/conversations/{_visitorId}/reply",
            new { body = "من پشتیبانی نیستم" });

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"expected 401/403, got {(int)response.StatusCode}");
    }
}
