using Bojan.Application.Common;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Storage;

/// <summary>
/// Writes a backup archive to disk, outside the tree <see cref="FileStorageOptions.PublicBaseUrl"/>
/// serves — see <see cref="IBackupArchiver"/> for why this must never be reachable the way an
/// uploaded product image is.
/// </summary>
public sealed class LocalBackupArchiver(IOptions<FileStorageOptions> options) : IBackupArchiver
{
    private readonly FileStorageOptions _options = options.Value;

    /// <summary>
    /// A sibling of <see cref="FileStorageOptions.RootPath"/>, not a folder
    /// under it. Whatever serves <c>RootPath</c> back out as
    /// <c>PublicBaseUrl</c> — this API in development, a reverse proxy or CDN
    /// origin in production — is handed that one directory to publish; a
    /// sibling directory is structurally outside anything it was ever told
    /// to serve, which a subfolder of the same root would not be.
    /// </summary>
    private string PrivateRoot =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_options.RootPath)) ?? ".", "backup-archives");

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(PrivateRoot);

        // The name is built entirely by the caller from a job id and a
        // timestamp — never a client-supplied string — so there is nothing
        // here to sanitise the way LocalFileStorage sanitises an uploaded
        // filename. Returned as-is: it is a reference private to this port,
        // not a URL anything outside it should construct or publish.
        await File.WriteAllBytesAsync(Path.Combine(PrivateRoot, fileName), content, cancellationToken);

        return fileName;
    }

    public Task<(string Reference, Stream Destination)> OpenWriteAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(PrivateRoot);

        // Same reasoning as SaveAsync: the name is built by the caller from a
        // job id and a timestamp, never from anything a client sent.
        var destination = new FileStream(
            Path.Combine(PrivateRoot, fileName),
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Task.FromResult<(string, Stream)>((fileName, destination));
    }

    public async Task<byte[]?> OpenReadAsync(string reference, CancellationToken cancellationToken)
    {
        var path = Resolve(reference);
        return path is not null && File.Exists(path)
            ? await File.ReadAllBytesAsync(path, cancellationToken)
            : null;
    }

    public Task<Stream?> OpenReadStreamAsync(string reference, CancellationToken cancellationToken)
    {
        var path = Resolve(reference);

        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true));
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken)
    {
        if (Resolve(reference) is { } path && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The path a stored reference names, or null when it is not one this
    /// archiver could have issued.
    /// </summary>
    /// <remarks>
    /// The reference is always a bare filename <see cref="SaveAsync"/> or
    /// <see cref="OpenWriteAsync"/> generated, but it arrives back here after a
    /// round trip through the database — treated as untrusted the same way any
    /// stored value crossing a trust boundary is, so a path separator smuggled
    /// into it cannot walk outside <see cref="PrivateRoot"/>.
    /// </remarks>
    private string? Resolve(string reference) =>
        reference.Contains('/') || reference.Contains('\\') || reference.Contains("..")
            ? null
            : Path.Combine(PrivateRoot, reference);
}
