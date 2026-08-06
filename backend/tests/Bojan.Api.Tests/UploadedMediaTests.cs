using System.Net;
using System.Net.Http.Headers;
using Bojan.Application.Common;
using Bojan.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// Uploads were accepted, written to disk and answered with a URL that nothing
/// served.
/// </summary>
/// <remarks>
/// No static-file middleware, no route, no rewrite — the address in
/// <c>Storage:PublicBaseUrl</c> pointed at the storefront, which has no
/// <c>/media</c> of its own, while the files sat in the API's volume. Every
/// product image, avatar, return photo and card-to-card receipt resolved to a
/// 404, including the receipts an owner is asked to inspect before crediting a
/// wallet.
/// </remarks>
public sealed class UploadedMediaTests : IDisposable
{
    private readonly BojanApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>The smallest valid GIF — enough to pass the storage adapter's magic-byte check.</summary>
    private static byte[] TinyGif() =>
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0x21, 0xF9, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B,
    ];

    private async Task<string> StoreAsync(string folder)
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        using var content = new MemoryStream(TinyGif());
        return await storage.SaveAsync(folder, "whatever-the-client-called-it.png", "image/gif", content, default);
    }

    [Fact]
    public async Task A_stored_file_is_reachable_at_the_url_the_upload_answered_with()
    {
        _factory.EnsureDatabaseCreated();
        var url = await StoreAsync("products");

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(PathOf(url));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/gif", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TinyGif(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task The_private_folders_are_served_too_so_an_operator_can_open_a_receipt()
    {
        _factory.EnsureDatabaseCreated();
        var url = await StoreAsync("receipts");

        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(PathOf(url))).StatusCode);
    }

    [Fact]
    public async Task Media_is_served_with_sniffing_refused_and_a_long_cache()
    {
        _factory.EnsureDatabaseCreated();
        var url = await StoreAsync("avatars");

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(PathOf(url));

        // These are files strangers uploaded.
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());

        var cache = response.Headers.CacheControl;
        Assert.True(cache?.Public);
        Assert.True(cache?.MaxAge > TimeSpan.FromDays(300));
    }

    [Fact]
    public async Task A_name_that_was_never_stored_is_a_404_rather_than_a_directory_listing()
    {
        _factory.EnsureDatabaseCreated();

        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/media/products/")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync("/media/products/00000000000000000000000000000000.jpg")).StatusCode);
    }

    /// <summary>The path part of the URL the storage adapter returns.</summary>
    private static string PathOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : url;
}
