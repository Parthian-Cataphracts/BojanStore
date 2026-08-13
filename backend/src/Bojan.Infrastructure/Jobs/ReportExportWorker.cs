using System.Text;
using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Drains the queue <c>POST /admin/reports/export</c> writes to.
/// </summary>
/// <remarks>
/// <see cref="ReportExport"/>'s own remarks say it plainly: "the row is the
/// queue... a worker fills in <c>FileUrl</c>." Nothing did — every export sat
/// at <see cref="JobStatus.Queued"/> forever, which is the general-purpose
/// task queue gap on this backend (what a Celery worker would be on a Python
/// one). This is the minimal, dependency-free answer: a hosted background
/// service polling the same queue table, not a new service to deploy. If the
/// job surface grows past report exports, that is the point to move to a
/// real broker (Hangfire is the closest .NET analogue) — for one queue with
/// a handful of rows a minute, polling is not a scaling problem yet.
/// </remarks>
public sealed class ReportExportWorker(
    IServiceScopeFactory scopeFactory, ILogger<ReportExportWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 5;

    /// <summary>
    /// How long a job may sit at <c>Running</c> before it is assumed abandoned.
    /// </summary>
    /// <remarks>
    /// Generous on purpose: the cost of reclaiming too early is generating a
    /// report twice, which overwrites its own file and wastes a little work.
    /// The cost of never reclaiming is a row stuck at "in progress" until
    /// somebody edits the database, which is what used to happen to every job
    /// in flight when the process went down.
    /// </remarks>
    private static readonly TimeSpan StalledAfter = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad row must not stop the worker from ever polling again.
                logger.LogError(ex, "Report export worker failed a poll cycle");
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

    private async Task ProcessQueuedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAdminRepository>();
        var queries = scope.ServiceProvider.GetRequiredService<IAdminQueries>();
        var archiver = scope.ServiceProvider.GetRequiredService<IBackupArchiver>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var reclaimed = await repository.ReclaimStalledJobsAsync(
            DateTimeOffset.UtcNow - StalledAfter, cancellationToken);

        if (reclaimed > 0)
        {
            logger.LogWarning("Put {Count} stalled job(s) back on the queue.", reclaimed);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var queued = await repository.ListQueuedReportExportsAsync(BatchSize, cancellationToken);
        if (queued.Count == 0) return;

        foreach (var export in queued)
        {
            export.Status = JobStatus.Running;
            export.StartedAtUtc = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var content = await BuildAsync(queries, export, cancellationToken);
                var fileName =
                    $"{export.Report}-{export.RequestedAtUtc:yyyyMMdd-HHmmss}-{export.Id:N}.{ExtensionFor(export.Format)}";
                // IBackupArchiver, not IFileStorage: the latter only accepts
                // the four image types it sniffs by magic bytes (it exists
                // for user uploads), and a CSV is neither an upload nor an
                // image. FileUrl here is the archiver's opaque reference, not
                // a clickable link — the download route below is the only
                // way back to the bytes, same as a backup archive.
                export.FileUrl = await archiver.SaveAsync(fileName, content, cancellationToken);
                export.Status = JobStatus.Completed;
                export.CompletedAtUtc = DateTimeOffset.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                export.Status = JobStatus.Failed;
                export.Error = ex.Message;
                logger.LogWarning(ex, "Report export {ExportId} ({Report}) failed", export.Id, export.Report);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Builds one report in the format it was asked for.
    /// </summary>
    /// <remarks>
    /// The report is fetched once and handed to whichever writer the format
    /// names. It used to be six calls to <c>CsvWriter</c> inline, which is why
    /// adding a second format looked like rewriting the whole method — the
    /// switch is over the report, and the format is a separate question about
    /// what to do with its rows.
    /// </remarks>
    private static async Task<byte[]> BuildAsync(
        IAdminQueries queries, ReportExport export, CancellationToken cancellationToken)
    {
        var from = export.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30);
        var to = export.ToUtc ?? DateTimeOffset.UtcNow;

        return export.Report switch
        {
            "sales" => Render(await queries.GetSalesAsync(from, to, ReportGrouping.Day, cancellationToken)),
            "orders" => Render(await queries.GetOrderStatusCountsAsync(from, to, cancellationToken)),
            "inventory" => Render([await queries.GetStockLevelsAsync(cancellationToken)]),
            "customers" => Render([await queries.GetCustomerSummaryAsync(cancellationToken)]),
            "campaigns" => Render(await queries.GetCampaignPerformanceAsync(from, to, cancellationToken)),
            "financial" => Render([await queries.GetFinancialTotalsAsync(from, to, cancellationToken)]),
            _ => throw new NotSupportedException($"گزارش «{export.Report}» شناخته نشد."),
        };

        byte[] Render<T>(IReadOnlyList<T> rows) => export.Format switch
        {
            ExportFormat.Csv => CsvWriter.Write(rows),
            ExportFormat.Xlsx => XlsxWriter.Write(rows),
            // PDF is the one still missing, and it is missing for a reason
            // worth stating: a PDF has to embed a font and shape Persian text
            // right to left, and a writer that does neither produces a file
            // full of disconnected reversed glyphs. That is worse than no PDF,
            // so the queue refuses the format up front rather than promising
            // one here.
            _ => throw new NotSupportedException($"فرمت {export.Format} هنوز پشتیبانی نمی‌شود."),
        };
    }

    /// <summary>The extension the built file carries, which is the format's own.</summary>
    private static string ExtensionFor(ExportFormat format) => format switch
    {
        ExportFormat.Xlsx => "xlsx",
        ExportFormat.Pdf => "pdf",
        _ => "csv",
    };
}
