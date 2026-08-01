using Bojan.Application.Common;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Directory the API writes uploads into. Served back under <see cref="PublicBaseUrl"/>.</summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>URL prefix the frontend will see, e.g. <c>/media</c> or a CDN origin.</summary>
    public string PublicBaseUrl { get; set; } = "/media";

    /// <summary>Ceiling per file. The panel's image picker and the return-photo form both sit well under this.</summary>
    public long MaxBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>
    /// What may be uploaded, by content type.
    /// </summary>
    /// <remarks>
    /// An allow-list, not a deny-list, and matched on the sniffed magic bytes
    /// rather than on the declared type or the file extension — a client
    /// controls both of those, and "image/png" on a file that is really a
    /// script is the oldest upload trick there is.
    /// </remarks>
    public IReadOnlyList<string> AllowedContentTypes { get; set; } =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];
}

/// <summary>
/// Through-the-API uploads onto local disk.
/// </summary>
/// <remarks>
/// <para>
/// <c>BACKEND.md</c> Phase 8: "Uploads are one shared decision, not six." This
/// is that decision made once — product images, avatars, return photos and B2B
/// attachments all arrive here, so swapping local disk for S3-compatible object
/// storage is one class, not six call sites.
/// </para>
/// <para>
/// Through-the-API rather than direct-to-storage with a signed URL, because the
/// content check below has to happen somewhere the client cannot skip. A signed
/// URL hands the client a direct write to the bucket, and then the only place
/// left to validate is a job that runs after the file is already public.
/// </para>
/// </remarks>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    /// <summary>
    /// The first bytes of each format the storage accepts.
    /// </summary>
    /// <remarks>
    /// WebP is checked on its <c>RIFF</c> container only — the <c>WEBP</c>
    /// marker sits at offset 8, past the shared prefix, and RIFF alone is
    /// enough to rule out the executable and script formats this list exists to
    /// keep out.
    /// </remarks>
    private static readonly (string ContentType, byte[] Magic)[] Signatures =
    [
        ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        ("image/gif", [0x47, 0x49, 0x46, 0x38]),
        ("image/webp", [0x52, 0x49, 0x46, 0x46]),
    ];

    public async Task<string> SaveAsync(
        string folder,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Content type '{contentType}' is not allowed.");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0 || buffer.Length > _options.MaxBytes)
        {
            throw new InvalidOperationException($"Upload must be between 1 byte and {_options.MaxBytes} bytes.");
        }

        var bytes = buffer.ToArray();
        var sniffed = Signatures.FirstOrDefault(signature =>
            bytes.Length >= signature.Magic.Length && bytes.Take(signature.Magic.Length).SequenceEqual(signature.Magic));

        if (sniffed.ContentType is null || !string.Equals(sniffed.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The file's contents do not match its declared type.");
        }

        // The client's filename is never used as a path: only its extension is
        // kept, and the name itself is generated. That is what stops
        // "../../appsettings.json" from being a filename.
        var extension = Path.GetExtension(fileName);
        if (extension.Length is 0 or > 10 || extension.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.'))
        {
            extension = ExtensionFor(contentType);
        }

        var safeFolder = string.Concat(folder.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
        var generated = $"{Guid.NewGuid():n}{extension.ToLowerInvariant()}";

        var directory = Path.Combine(_options.RootPath, safeFolder);
        Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(Path.Combine(directory, generated), bytes, cancellationToken);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{safeFolder}/{generated}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken)
    {
        var prefix = _options.PublicBaseUrl.TrimEnd('/');
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            // Not ours to delete — a URL pointing at a CDN or an external host
            // arrives here only by mistake.
            return Task.CompletedTask;
        }

        var relative = url[prefix.Length..].TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_options.RootPath, relative));
        var root = Path.GetFullPath(_options.RootPath);

        // Re-checked after resolving: a stored URL should never escape the
        // upload root, and if one somehow does, deleting it is the last thing
        // this should do.
        if (full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".jpg",
    };
}
