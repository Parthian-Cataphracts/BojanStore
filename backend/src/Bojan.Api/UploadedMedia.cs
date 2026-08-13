using Bojan.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bojan.Api;

/// <summary>
/// Serves the files <see cref="LocalFileStorage"/> writes.
/// </summary>
/// <remarks>
/// <para>
/// Nothing did. Uploads were accepted, sniffed, written to disk and answered
/// with a URL under <c>Storage:PublicBaseUrl</c> — and no route, rewrite or
/// static-file middleware existed anywhere to answer it. Every product image,
/// avatar, return photo and card-to-card receipt resolved to a 404, including
/// the receipts an owner is asked to inspect before releasing money.
/// </para>
/// <para>
/// Served from the API rather than proxied through the storefront because this
/// is the process the volume is mounted into; the compose file's
/// <c>Storage__PublicBaseUrl</c> points here for the same reason.
/// </para>
/// <para>
/// The names are the security boundary. <see cref="LocalFileStorage"/> discards
/// the client's filename entirely and generates a 128-bit random one, so a URL
/// under this path is a capability: unguessable, and only ever handed to
/// someone entitled to it. That is the model a pre-signed object-storage URL
/// uses, and it is what makes it acceptable to serve the private folders
/// (<c>returns</c>, <c>receipts</c>) from the same place as the public ones.
/// Anything stronger means per-object authorisation, which is a change to how
/// the panel and the account screens link to media, not to this file.
/// </para>
/// </remarks>
internal static class UploadedMedia
{
    public static void UseUploadedMedia(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;

        // The path the public base URL ends in — "/media" by default. Taken
        // from configuration so the two cannot drift: a base URL pointing at
        // one path while the middleware answers another is exactly the silence
        // this replaces.
        var requestPath = MediaPathOf(storage.PublicBaseUrl);
        if (requestPath is null)
        {
            // An absolute base URL on another host means something else serves
            // the media — a CDN, an object store. Nothing to mount here.
            app.Logger.LogInformation(
                "Storage:PublicBaseUrl is {BaseUrl}; uploaded media is expected to be served by that host, not this one.",
                storage.PublicBaseUrl);
            return;
        }

        // Resolved exactly as `LocalFileStorage` resolves it — bare
        // `Path.GetFullPath`, so a relative path is relative to the working
        // directory. That is not an arbitrary choice between two bases: the
        // writer picks one, and a reader that picks the other serves an empty
        // directory while the files sit where they were put.
        //
        // There was a second registration of this same directory in Program.cs
        // that resolved against `ContentRootPath`. Identical whenever the path
        // is absolute, which is what the compose file sets, and a different
        // folder the moment it is not. It is gone; this is the one mount.
        var root = Path.GetFullPath(storage.RootPath);
        Directory.CreateDirectory(root);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = requestPath,

            // Left at its default of false on purpose. Only extensions the
            // content-type provider knows are served at all, and the storage
            // adapter derives the extension from the file's own sniffed magic
            // bytes rather than from anything the client said — so the set on
            // disk is the four image types and nothing else.
            ServeUnknownFileTypes = false,

            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.GetTypedHeaders();

                // The name contains a GUID and the bytes never change under it,
                // so this is immutable in the strict sense.
                headers.CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(365),
                };

                // These are files strangers uploaded. Refusing content sniffing
                // is what stops a browser deciding an image is something it can
                // execute.
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Context.Response.Headers.ContentDisposition = "inline";
            },
        });
    }

    /// <summary>
    /// The path part of <paramref name="publicBaseUrl"/>, or null when it names
    /// another host.
    /// </summary>
    private static string? MediaPathOf(string publicBaseUrl)
    {
        var trimmed = publicBaseUrl.TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            var path = absolute.AbsolutePath.TrimEnd('/');
            return path.Length > 1 ? path : null;
        }

        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }
}
