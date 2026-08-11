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
    ///
    /// GIF is here and not in the panel's file picker, deliberately: an animated
    /// stamp is not something to offer, but a still GIF someone already has is no
    /// less safe than a PNG.
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
    /// WebP needs both halves of its header. <c>RIFF</c> alone is the container
    /// AVI and WAV also use, so matching on it would accept those as images;
    /// the <c>WEBP</c> marker at offset 8 is what distinguishes the format, and
    /// the four bytes between the two are the length field, which is why this
    /// is expressed as two runs rather than one prefix.
    /// </remarks>
    private static readonly (string ContentType, (int Offset, byte[] Bytes)[] Runs)[] Signatures =
    [
        ("image/jpeg", [(0, [0xFF, 0xD8, 0xFF])]),
        ("image/png", [(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])]),
        ("image/gif", [(0, [0x47, 0x49, 0x46, 0x38])]),
        ("image/webp", [(0, [0x52, 0x49, 0x46, 0x46]), (8, [0x57, 0x45, 0x42, 0x50])]),
    ];

    private static bool Matches(byte[] bytes, (int Offset, byte[] Bytes)[] runs) =>
        runs.All(run =>
            bytes.Length >= run.Offset + run.Bytes.Length &&
            bytes.AsSpan(run.Offset, run.Bytes.Length).SequenceEqual(run.Bytes));

    /// <param name="fileName">Discarded — see below for why nothing the client named survives.</param>
    /// <param name="contentType">
    /// Also discarded. It is on the port because callers have it, not because
    /// this trusts it: what the file is gets decided by reading it.
    /// </param>
    public async Task<string> SaveAsync(
        string folder,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        // Read one byte past the ceiling and no further.
        //
        // The size check used to come after copying the whole stream into
        // memory, which meant the way to make this server hold a large
        // allocation was to send one — the check rejected it only once it was
        // already buffered. Kestrel's own 30 MB request cap bounded that, but
        // "bounded by a limit nobody set deliberately" is not the same as
        // bounded. Stopping at MaxBytes + 1 is enough to know the file is too
        // big without ever holding more than that.
        using var buffer = new MemoryStream();
        var ceiling = _options.MaxBytes + 1;
        var chunk = new byte[81_920];

        while (buffer.Length < ceiling)
        {
            var read = await content.ReadAsync(
                chunk.AsMemory(0, (int)Math.Min(chunk.Length, ceiling - buffer.Length)),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0 || buffer.Length > _options.MaxBytes)
        {
            throw new UploadRejectedException(
                UploadRejection.Size,
                $"Upload must be between 1 byte and {_options.MaxBytes} bytes.");
        }

        var bytes = buffer.ToArray();
        var sniffed = Signatures.FirstOrDefault(signature => Matches(bytes, signature.Runs));

        // Judged on what the file *is*, never on what the request said it is.
        //
        // This used to also require the declared type to match the sniffed one,
        // which protected nothing — the sniff is what makes the file safe, and a
        // client that lies about the header is caught by it either way — while
        // rejecting perfectly good images for a reason no operator could see.
        // Windows serves that header out of the registry, and any image editor
        // that has ever claimed a file association can leave a `.png` announcing
        // itself as `image/x-png` or a `.jpg` as `image/pjpeg`. The shop's own
        // stamp is exactly the kind of file that has been through such an editor.
        if (sniffed.ContentType is null ||
            !_options.AllowedContentTypes.Contains(sniffed.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UploadRejectedException(
                UploadRejection.Type,
                "The file is not one of the image formats this shop accepts.");
        }

        // The client's filename is discarded entirely — neither its path nor
        // its extension survives. That stops "../../appsettings.json" from
        // being a filename, and it stops the subtler version: a real JPEG
        // (correct magic bytes, `image/jpeg` declared) uploaded as ".html" so
        // that whatever serves the media root hands it back as HTML. Image
        // formats carry arbitrary bytes in their comment and EXIF segments, so
        // such a file is a valid image *and* a script. The extension is derived
        // from the sniffed type, which is the only type here that was proven.
        var extension = ExtensionFor(sniffed.ContentType);

        var safeFolder = string.Concat(folder.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
        var generated = $"{Guid.NewGuid():n}{extension.ToLowerInvariant()}";

        var directory = Path.Combine(_options.RootPath, safeFolder);
        Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(Path.Combine(directory, generated), bytes, cancellationToken);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{safeFolder}/{generated}";
    }

    public bool IsOwnUrl(string url, string folder)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Built the same way SaveAsync builds what it returns, so the two
        // cannot drift: same base, same folder, and then a generated name.
        var expected = $"{_options.PublicBaseUrl.TrimEnd('/')}/{folder}/";
        if (!url.StartsWith(expected, StringComparison.Ordinal))
        {
            return false;
        }

        // Exactly one path segment after the folder, and it has to look like
        // the name SaveAsync generates — a 32-character hex guid plus a known
        // extension. "…/avatars/../../secret.png" never reaches here.
        var name = url[expected.Length..];
        var dot = name.LastIndexOf('.');

        return dot == 32
            && name.Length > dot + 1
            && name[..dot].All(char.IsAsciiHexDigitLower)
            && name[(dot + 1)..].All(char.IsAsciiLetterOrDigit);
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

        // The trailing separator is not cosmetic: comparing against a bare
        // "uploads" would also accept "uploads-backup/…", which resolves
        // outside the root while still passing a plain prefix test.
        var root = Path.GetFullPath(_options.RootPath);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

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
