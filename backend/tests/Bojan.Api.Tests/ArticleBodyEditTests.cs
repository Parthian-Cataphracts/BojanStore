using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Saving an article a second time.
/// </summary>
/// <remarks>
/// <para>
/// This was refused with a conflict, so no article in the magazine could be
/// edited at all: creating one worked, and every save after that returned a
/// 409 the operator could do nothing about.
/// </para>
/// <para>
/// The cause is the one <see cref="ProductGalleryEditTests"/> describes — an
/// id assigned in <c>Entity</c>'s own initialiser is read by EF as proof the
/// row exists, so a block built fresh was written as an update to a row that
/// had never been inserted. An article's body is replaced wholesale on every
/// save, which means every block is built fresh, which means every save after
/// the first failed.
/// </para>
/// </remarks>
public sealed class ArticleBodyEditTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "article-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_article_can_be_edited_after_it_is_created()
    {
        var id = await CreateAsync();

        var edited = await _client.PostAsJsonAsync(
            "/api/admin/articles",
            new { id, body = "پاراگراف اول\n\nپاراگراف دوم" });

        edited.EnsureSuccessStatusCode();

        var blocks = await BlocksAsync(Guid.Parse(id));
        Assert.Equal(["پاراگراف اول", "پاراگراف دوم"], blocks);
    }

    [Fact]
    public async Task Editing_it_again_replaces_the_body_rather_than_appending_to_it()
    {
        var id = await CreateAsync();

        await _client.PostAsJsonAsync("/api/admin/articles", new { id, body = "یک\n\nدو\n\nسه" });
        await _client.PostAsJsonAsync("/api/admin/articles", new { id, body = "فقط یکی" });

        Assert.Equal(["فقط یکی"], await BlocksAsync(Guid.Parse(id)));
    }

    private async Task<string> CreateAsync()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/admin/articles",
            new { title = "مقاله آزمایشی", body = "پاراگراف اول" });

        created.EnsureSuccessStatusCode();

        var saved = await created.Content.ReadFromJsonAsync<SavedId>();
        Assert.NotNull(saved);
        return saved.Id;
    }

    private Task<List<string>> BlocksAsync(Guid articleId) =>
        _factory.WithDbAsync(async db => await db.ArticleBlocks.AsNoTracking()
            .Where(block => block.ArticleId == articleId)
            .OrderBy(block => block.SortOrder)
            .Select(block => block.Text!)
            .ToListAsync());

    private sealed record SavedId(string Id);
}
