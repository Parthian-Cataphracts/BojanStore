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

        var queued = await repository.ListQueuedReportExportsAsync(BatchSize, cancellationToken);
        if (queued.Count == 0) return;

        foreach (var export in queued)
        {
            export.Status = JobStatus.Running;
            await unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                if (export.Format != ExportFormat.Csv)
                {
                    // XLSX/PDF writers are a real piece of work each, not a
                    // rename of this one — failing clearly beats a ".xlsx"
                    // file that is secretly CSV with the wrong extension.
                    throw new NotSupportedException($"فرمت {export.Format} هنوز پشتیبانی نمی‌شود.");
                }

                var csv = await BuildCsvAsync(queries, export, cancellationToken);
                var fileName = $"{export.Report}-{export.RequestedAtUtc:yyyyMMdd-HHmmss}-{export.Id:N}.csv";
                // IBackupArchiver, not IFileStorage: the latter only accepts
                // the four image types it sniffs by magic bytes (it exists
                // for user uploads), and a CSV is neither an upload nor an
                // image. FileUrl here is the archiver's opaque reference, not
                // a clickable link — the download route below is the only
                // way back to the bytes, same as a backup archive.
                export.FileUrl = await archiver.SaveAsync(fileName, csv, cancellationToken);
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

    private static async Task<byte[]> BuildCsvAsync(
        IAdminQueries queries, ReportExport export, CancellationToken cancellationToken)
    {
        var from = export.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30);
        var to = export.ToUtc ?? DateTimeOffset.UtcNow;

        return export.Report switch
        {
            "sales" => ToCsv(await queries.GetSalesAsync(from, to, ReportGrouping.Day, cancellationToken)),
            "orders" => ToCsv(await queries.GetOrderStatusCountsAsync(from, to, cancellationToken)),
            "inventory" => ToCsv([await queries.GetStockLevelsAsync(cancellationToken)]),
            "customers" => ToCsv([await queries.GetCustomerSummaryAsync(cancellationToken)]),
            "campaigns" => ToCsv(await queries.GetCampaignPerformanceAsync(from, to, cancellationToken)),
            "financial" => ToCsv([await queries.GetFinancialTotalsAsync(from, to, cancellationToken)]),
            _ => throw new NotSupportedException($"گزارش «{export.Report}» شناخته نشد."),
        };
    }

    /// <summary>
    /// Every report DTO to a CSV, one column per public property, by
    /// reflection — six bespoke writers for six flat DTOs would be six
    /// places to keep in sync with <c>AdminContracts.cs</c> for no benefit
    /// over reading the shape directly off the type.
    /// </summary>
    private static byte[] ToCsv<T>(IReadOnlyList<T> rows)
    {
        var properties = typeof(T).GetProperties();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", properties.Select(p => p.Name)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", properties.Select(p => CsvField(p.GetValue(row)))));
        }

        // UTF-8 BOM so Excel opens Persian text as UTF-8 instead of guessing
        // the system codepage and mangling it.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static string CsvField(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTimeOffset dt => dt.ToString("yyyy-MM-dd HH:mm"),
            _ => value.ToString() ?? string.Empty,
        };

        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
