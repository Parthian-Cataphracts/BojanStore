using Bojan.Application.Common;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Sends the broadcasts <c>POST /admin/notifications</c> queues.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="INotificationDispatcher"/> was registered and never called by
/// anything. What that meant in practice: a campaign scheduled for later was
/// stored and never sent — there was nothing in the process that would ever
/// look at it again — and an SMS broadcast was never sent at all, because the
/// only code that delivered anything was the in-app fan-out inside the request
/// that queued it. The panel reported both as sent.
/// </para>
/// <para>
/// Same shape as <see cref="ReportExportWorker"/> and for the same reason: one
/// hosted service polling the table the write already produces, rather than a
/// broker to deploy. <see cref="INotificationDispatcher.DispatchAsync"/> is
/// idempotent on the campaign's sent stamp, so a poll that overlaps a previous
/// one cannot send the same offer twice.
/// </para>
/// </remarks>
public sealed class NotificationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Campaigns per cycle.
    /// </summary>
    /// <remarks>
    /// Small because each one fans out to an entire audience. Taking a dozen at
    /// once would put a dozen full fan-outs in one cycle, which is the thing the
    /// dispatcher's own batching exists to avoid.
    /// </remarks>
    private const int BatchSize = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One campaign that cannot be delivered must not stop every
                // later one from being looked at.
                logger.LogError(ex, "Notification worker failed a poll cycle");
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

    private async Task SendDueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        // Taken from the scope, not injected. A hosted service is a singleton
        // and the clock is scoped, so holding one would fail service validation
        // at startup — which is exactly what it did.
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;

        var due = await db.NotificationCampaigns.AsNoTracking()
            .Where(c => c.SentAtUtc == null)
            // Unscheduled means "now"; scheduled means when it comes round.
            .Where(c => c.ScheduledAtUtc == null || c.ScheduledAtUtc <= now)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(BatchSize)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in due)
        {
            try
            {
                await dispatcher.DispatchAsync(id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Left unsent so the next cycle retries it, rather than stamped
                // sent to get it out of the queue — a campaign nobody received
                // must not read as delivered.
                logger.LogError(ex, "Notification campaign {CampaignId} could not be dispatched", id);
            }
        }
    }
}
