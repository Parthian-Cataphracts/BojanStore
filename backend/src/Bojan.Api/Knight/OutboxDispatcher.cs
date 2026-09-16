using System.Text.Json;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Outbox;
using Knight.StoreAgent;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Knight;

/// <summary>
/// Delivers queued store events to KNIGHT's feature services, and keeps trying.
///
/// It reads the due Pending rows, hands each to the agent's forwarder, and marks
/// it Delivered only when every subscriber accepted it. A row that fails backs off
/// and is tried again — across restarts, because the queue is in the database — so
/// a feature service that was down catches up rather than missing the event. After
/// enough attempts a row is parked as Failed for an operator to see, rather than
/// retried forever.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;
    private const int MaxAttempts = 12;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A bad pass must never take the dispatcher down; the next tick retries.
                logger.LogError(exception, "An outbox dispatch pass failed; it will run again.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchDueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var forwarder = scope.ServiceProvider.GetRequiredService<IKnightEventForwarder>();

        var now = DateTimeOffset.UtcNow;
        var due = await db.OutboxEvents
            .Where(e => e.Status == OutboxStatus.Pending && e.NextAttemptUtc <= now)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var row in due)
        {
            var delivered = false;
            try
            {
                using var document = JsonDocument.Parse(row.PayloadJson);
                delivered = await forwarder.ForwardAsync(row.EventName, document.RootElement.Clone(), cancellationToken);
            }
            catch (Exception exception)
            {
                row.LastError = exception.Message;
            }

            if (delivered)
            {
                row.Status = OutboxStatus.Delivered;
                row.DeliveredAtUtc = DateTimeOffset.UtcNow;
                row.LastError = null;
            }
            else
            {
                row.Attempts += 1;
                if (row.Attempts >= MaxAttempts)
                {
                    row.Status = OutboxStatus.Failed;
                    logger.LogError(
                        "Outbox event {Id} ({Event}) parked as Failed after {Attempts} attempts.",
                        row.Id, row.EventName, row.Attempts);
                }
                else
                {
                    // Exponential backoff, capped: 10s, 20s, 40s … up to five minutes.
                    var seconds = Math.Min(MaxBackoff.TotalSeconds, PollInterval.TotalSeconds * Math.Pow(2, row.Attempts));
                    row.NextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
