using System.IO.Compression;
using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Takes the backups screen 156 asks for.
/// </summary>
/// <remarks>
/// <para>
/// What this replaces reported success over nothing. The old implementation
/// wrote a small JSON file naming the job — its id, its kind, who asked and
/// when — marked the row <c>Completed</c>, and gave the panel a download link.
/// No database was dumped and no uploaded file was archived. An operator who
/// pressed the button got every signal that they were protected and none of the
/// protection, which is worse than not having the feature: it is the difference
/// between knowing you have no backup and finding out at a restore.
/// </para>
/// <para>
/// Backups run here rather than inline in the request because a real one takes
/// minutes, not milliseconds, and a request that holds a connection open for
/// that long is a request that times out at the proxy while the work is still
/// going. The job row is the queue, the same as report exports, and the same
/// reclaim puts back anything a restart abandoned.
/// </para>
/// <para>
/// A failure is recorded as a failure, with the reason. Nothing here has a
/// fallback that produces a smaller file and calls it done.
/// </para>
/// </remarks>
public sealed class BackupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BackupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>The three kinds screen 156 offers.</summary>
    public const string DatabaseKind = "database";
    public const string MediaKind = "media";
    public const string FullKind = "full";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Backup worker failed a poll cycle");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Takes the next backup off the queue, if there is one.
    /// </summary>
    /// <remarks>
    /// Public so a test can drive one cycle directly. The polling loop above is
    /// what calls it in production, and a five-second wait is not something a
    /// test should have to sit through to find out whether the archive it asked
    /// for was built.
    /// </remarks>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAdminRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // One at a time. A dump is heavy on the database and on the disk, and
        // two of them racing is how a backup becomes the outage.
        var job = await repository.FindNextQueuedBackupAsync(cancellationToken);
        if (job is null) return;

        job.Status = JobStatus.Running;
        job.StartedAtUtc = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var archiver = scope.ServiceProvider.GetRequiredService<IBackupArchiver>();
        string? reference = null;

        try
        {
            var fileName = $"{job.Kind}-{job.RequestedAtUtc:yyyyMMdd-HHmmss}-{job.Id:N}.zip";
            (reference, var destination) = await archiver.OpenWriteAsync(fileName, cancellationToken);

            await using (destination)
            {
                await WriteArchiveAsync(scope.ServiceProvider, job.Kind, destination, cancellationToken);
            }

            job.ArchiveReference = reference;
            job.SizeBytes = (await archiver.OpenReadStreamAsync(reference, cancellationToken)) is { } sized
                ? await LengthOfAsync(sized)
                : null;
            job.Status = JobStatus.Completed;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Backup {JobId} ({Kind}) completed, {Bytes} bytes.", job.Id, job.Kind, job.SizeBytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The part-written archive goes: a truncated zip on disk with a row
            // pointing at it is the same false reassurance this worker exists to
            // remove.
            if (reference is not null)
            {
                try
                {
                    await archiver.DeleteAsync(reference, CancellationToken.None);
                }
                catch (IOException cleanup)
                {
                    logger.LogWarning(cleanup, "Could not remove the partial archive for backup {JobId}.", job.Id);
                }
            }

            job.ArchiveReference = null;
            job.SizeBytes = null;
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            logger.LogError(ex, "Backup {JobId} ({Kind}) failed", job.Id, job.Kind);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task<long> LengthOfAsync(Stream stream)
    {
        await using (stream)
        {
            return stream.Length;
        }
    }

    /// <summary>
    /// Builds the archive for one job kind.
    /// </summary>
    /// <remarks>
    /// A zip with the dump and the media tree inside it, rather than two files:
    /// a "full" backup that arrives as two downloads is two things an operator
    /// has to keep together, and the pairing is exactly what is lost first.
    /// </remarks>
    private static async Task WriteArchiveAsync(
        IServiceProvider services,
        string kind,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        if (kind is DatabaseKind or FullKind)
        {
            var dumper = services.GetRequiredService<IDatabaseDumper>();

            if (!await dumper.IsAvailableAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "ابزار pg_dump روی سرور در دسترس نیست، پس پشتیبان پایگاه‌داده گرفته نشد.");
            }

            // Already compressed by pg_dump's custom format, so storing it
            // rather than deflating it again saves the CPU and costs nothing.
            var entry = archive.CreateEntry("database.dump", CompressionLevel.NoCompression);
            await using var entryStream = entry.Open();
            await dumper.DumpAsync(entryStream, cancellationToken);
        }

        if (kind is MediaKind or FullKind)
        {
            var storage = services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            await AddMediaAsync(archive, storage.RootPath, cancellationToken);
        }

        if (kind is not (DatabaseKind or MediaKind or FullKind))
        {
            throw new InvalidOperationException($"نوع پشتیبان «{kind}» شناخته نشد.");
        }
    }

    /// <summary>Copies the uploads tree into the archive, keeping its shape.</summary>
    /// <remarks>
    /// An empty uploads directory is not an error — a shop that has not
    /// uploaded anything has nothing to archive, and failing the job over it
    /// would train operators to ignore failures.
    /// </remarks>
    private static async Task AddMediaAsync(
        ZipArchive archive,
        string rootPath,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root)) return;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var entry = archive.CreateEntry($"media/{relative}", CompressionLevel.Optimal);

            await using var entryStream = entry.Open();
            await using var source = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);

            await source.CopyToAsync(entryStream, cancellationToken);
        }
    }
}
