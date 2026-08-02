using Bojan.Application.Common;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Storage;

/// <summary>
/// Writes a backup archive next to the uploads root, under its own folder —
/// see <see cref="IBackupArchiver"/> for why this does not go through
/// <see cref="LocalFileStorage"/>.
/// </summary>
public sealed class LocalBackupArchiver(IOptions<FileStorageOptions> options) : IBackupArchiver
{
    private readonly FileStorageOptions _options = options.Value;
    private const string Folder = "backups";

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_options.RootPath, Folder);
        Directory.CreateDirectory(directory);

        // The name is built entirely by the caller from a job id and a
        // timestamp — never a client-supplied string — so there is nothing
        // here to sanitise the way LocalFileStorage sanitises an uploaded
        // filename.
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), content, cancellationToken);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{Folder}/{fileName}";
    }
}
