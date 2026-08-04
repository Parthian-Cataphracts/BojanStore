using System.Security.Cryptography;
using System.Text.Json;
using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Support;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Customers;
using Bojan.Domain.Marketing;
using Bojan.Domain.Orders;
using Bojan.Domain.Support;

namespace Bojan.Application.Administration;

/// <summary>
/// The panel's operational writes — orders, B2B, support, broadcasts, exports,
/// settings, keys and the operator's own credentials.
/// </summary>
/// <remarks>
/// Split from <see cref="AdminCatalogueService"/> along the same line the panel
/// splits its own screens: one service edits what the shop sells, this one
/// runs the shop. Both audit every write.
/// </remarks>
public sealed class AdminOperationsService(
    IAdminRepository repository,
    ISupportRepository support,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Moves an order on and tells the customer.
    /// </summary>
    /// <remarks>
    /// <c>BACKEND.md</c> Phase 7 leaves open whether the transition or a
    /// separate job sends the notification. It is done here, in the same
    /// transaction: an order that moved to <c>shipped</c> without the customer
    /// being told is a support ticket, and a queue that can drop the message is
    /// a worse trade than a transaction that is one row longer.
    /// </remarks>
    public async Task<UseCaseResult> UpdateOrderStatusAsync(OrderStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        if (WireFormat.ParseOrderStatus(request.Status) is not { } status)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "status");
        }

        var order = await repository.FindOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        try
        {
            repository.AddOrderTimelineEvent(order.TransitionTo(status, request.TrackingCode));
        }
        catch (InvalidOperationException)
        {
            // The order is in a terminal state. That is the operator asking for
            // something the domain forbids, not a server fault.
            return UseCaseResult.Failure(UseCaseError.Conflict, "terminal-status");
        }

        if (request.Note is { Length: > 0 })
        {
            order.Note = request.Note;
        }

        repository.AddCustomerNotification(new CustomerNotification
        {
            CustomerId = order.CustomerId,
            Kind = NotificationKind.Order,
            Title = $"سفارش {order.Number}",
            Body = StatusMessage(status, order.Number),
            Href = $"/account/orders/{order.Id}",
            CreatedAtUtc = clock.UtcNow,
        });

        audit.Record("order.status.changed", order.Number);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<UseCaseResult> UpdateBusinessRequestAsync(BusinessRequestUpdate request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var businessRequest = await repository.FindBusinessRequestAsync(id, cancellationToken);
        if (businessRequest is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (request.Status is not null)
        {
            if (!Enum.TryParse<BusinessRequestStatus>(request.Status, ignoreCase: true, out var status))
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "status");
            }

            try
            {
                repository.AddBusinessRequestEvent(businessRequest.TransitionTo(status, clock.UtcNow));
            }
            catch (InvalidOperationException)
            {
                return UseCaseResult.Failure(UseCaseError.Conflict, "terminal-status");
            }
        }

        if (request.AssigneeId is { } assignee)
        {
            businessRequest.AssigneeId = assignee.Length == 0
                ? null
                : Guid.TryParse(assignee, out var assigneeId) ? assigneeId : businessRequest.AssigneeId;
        }

        if (request.Note is not null)
        {
            businessRequest.InternalNote = request.Note;
        }

        audit.Record("business-request.updated", businessRequest.Code);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<UseCaseResult> ReplyToThreadAsync(SupportReplyRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ThreadId, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "threadId");
        }

        var ticket = await support.FindTicketWithMessagesAsync(id, cancellationToken);
        if (ticket is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        support.AddMessage(ticket.AddMessage(request.Body, fromSupport: true, clock.UtcNow));

        if (ticket.CustomerId is { } customerId)
        {
            repository.AddCustomerNotification(new CustomerNotification
            {
                CustomerId = customerId,
                Kind = NotificationKind.Account,
                Title = "پاسخ پشتیبانی",
                Body = ticket.Subject,
                Href = $"/account/support/{ticket.Id}",
                CreatedAtUtc = clock.UtcNow,
            });
        }

        audit.Record("support.replied", ticket.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<UseCaseResult<string>> SaveCannedReplyAsync(CannedReplyRequest request, CancellationToken cancellationToken)
    {
        CannedReply reply;

        if (Guid.TryParse(request.Id, out var id))
        {
            var existing = await support.FindCannedReplyAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            reply = existing;

            if (request.Deleted == true)
            {
                reply.SoftDelete(clock.UtcNow);
                audit.Record("support.canned-reply.deleted", reply.Title);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return reply.Id.ToString();
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            reply = new CannedReply { Title = request.Title, Body = request.Body };
            support.AddCannedReply(reply);
        }

        if (request.Title is not null) reply.Title = request.Title;
        if (request.Body is not null) reply.Body = request.Body;
        reply.UpdatedAtUtc = clock.UtcNow;

        audit.Record("support.canned-reply.saved", reply.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return reply.Id.ToString();
    }

    /// <summary>
    /// Queues a broadcast and, when it is due now and in-app, writes the
    /// per-customer rows screen 53 reads.
    /// </summary>
    /// <remarks>
    /// A scheduled broadcast, or one on a channel that leaves the system, is
    /// left for <see cref="INotificationDispatcher"/> — fanning out to every
    /// customer inside the operator's request would make the panel wait on the
    /// size of the audience.
    /// </remarks>
    public async Task<UseCaseResult<string>> QueueBroadcastAsync(
        Guid actorId,
        BroadcastRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<NotificationChannel>(request.Channel.Replace("-", string.Empty), ignoreCase: true, out var channel))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "channel");
        }

        var campaign = new NotificationCampaign
        {
            Channel = channel,
            Audience = request.Audience,
            Title = request.Title,
            Body = request.Body,
            ScheduledAtUtc = request.ScheduledAt,
            ActorId = actorId,
            CreatedAtUtc = clock.UtcNow,
        };

        repository.AddNotificationCampaign(campaign);

        var dueNow = request.ScheduledAt is null || request.ScheduledAt <= clock.UtcNow;

        if (dueNow && channel == NotificationChannel.InApp)
        {
            foreach (var customerId in await repository.ListCustomerIdsAsync(request.Audience, cancellationToken))
            {
                repository.AddCustomerNotification(new CustomerNotification
                {
                    CustomerId = customerId,
                    Kind = NotificationKind.Offer,
                    Title = request.Title,
                    Body = request.Body,
                    CreatedAtUtc = clock.UtcNow,
                });
            }

            campaign.SentAtUtc = clock.UtcNow;
        }

        audit.Record("notification.queued", request.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return campaign.Id.ToString();
    }

    /// <summary>
    /// Queues an export. Returns as soon as the row exists —
    /// <c>BACKEND.md</c> Phase 7: "it is not a synchronous download."
    /// </summary>
    public async Task<UseCaseResult<string>> QueueReportExportAsync(
        Guid actorId,
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ExportFormat>(request.Format ?? "csv", ignoreCase: true, out var format))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "format");
        }

        var export = new ReportExport
        {
            Report = request.Report,
            Format = format,
            FromUtc = request.From,
            ToUtc = request.To,
            RequestedById = actorId,
            RequestedAtUtc = clock.UtcNow,
        };

        repository.AddReportExport(export);
        audit.Record("report.export.queued", request.Report);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return export.Id.ToString();
    }

    public async Task<UseCaseResult<string>> QueueBackupAsync(
        Guid actorId,
        BackupRequest request,
        CancellationToken cancellationToken)
    {
        // The panel asks for an explicit confirmation on screen 156; a backup
        // request that arrives without it is a misclick or a crafted body.
        if (!request.Confirm)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "confirm");
        }

        var job = new BackupJob
        {
            Kind = request.Kind,
            RequestedById = actorId,
            RequestedAtUtc = clock.UtcNow,
        };

        repository.AddBackupJob(job);
        audit.Record("backup.queued", request.Kind);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return job.Id.ToString();
    }

    public async Task<UseCaseResult> SaveSettingsAsync(
        Guid actorId,
        SettingsRequest request,
        CancellationToken cancellationToken)
    {
        foreach (var (key, value) in request.Values)
        {
            var existing = await repository.FindSettingAsync(request.Section, key, cancellationToken);

            if (existing is null)
            {
                repository.AddSetting(new SettingEntry
                {
                    Section = request.Section,
                    Key = key,
                    Value = value,
                    UpdatedAtUtc = clock.UtcNow,
                    UpdatedById = actorId,
                });
            }
            else
            {
                existing.Value = value;
                existing.UpdatedAtUtc = clock.UtcNow;
                existing.UpdatedById = actorId;
            }
        }

        audit.Record("settings.saved", request.Section);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Creates or revokes an API key.
    /// </summary>
    /// <remarks>
    /// The plaintext key is returned exactly once, from this call, and only its
    /// hash is stored — the same reason a password hash is one-way. A caller
    /// who loses it issues a new key rather than reading the old one back.
    /// </remarks>
    public async Task<UseCaseResult<CreatedApiKeyDto?>> SaveApiKeyAsync(
        Guid actorId,
        ApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(request.Id, out var id))
        {
            var existing = await repository.FindApiKeyAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<CreatedApiKeyDto?>.Failure(UseCaseError.NotFound);

            if (request.Label is not null) existing.Label = request.Label;
            if (request.Scope is not null) existing.Scope = request.Scope;
            if (request.Revoked == true) existing.RevokedAtUtc = clock.UtcNow;
            else if (request.Revoked == false) existing.RevokedAtUtc = null;

            audit.Record("api-key.updated", existing.Label);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return UseCaseResult<CreatedApiKeyDto?>.Success(null);
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return UseCaseResult<CreatedApiKeyDto?>.Failure(UseCaseError.Invalid, "label");
        }

        var secret = $"bjn_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";

        var key = new ApiKey
        {
            Label = request.Label,
            KeyHash = HashKey(secret),
            Prefix = secret[..12],
            Scope = request.Scope ?? "read",
            CreatedById = actorId,
            CreatedAtUtc = clock.UtcNow,
        };

        repository.AddApiKey(key);
        audit.Record("api-key.created", key.Label);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<CreatedApiKeyDto?>.Success(
            new CreatedApiKeyDto(key.Id.ToString(), key.Label, key.Prefix, key.Scope, secret));
    }

    public async Task<UseCaseResult> ChangePasswordAsync(
        Guid adminId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await repository.FindAdminUserAsync(adminId, cancellationToken);
        if (admin is null)
        {
            return UseCaseResult.Failure(UseCaseError.Unauthorized);
        }

        if (!passwordHasher.Verify(request.CurrentPassword, admin.PasswordHash))
        {
            return UseCaseResult.Failure(UseCaseError.Forbidden, "current-password");
        }

        // The same floor the request validator applies before this runs, and
        // the same one the panel's own form checks against. Kept here as well
        // because a use case that only holds when something upstream remembers
        // to validate is not holding it.
        if (request.NewPassword.Length < MinimumPasswordLength)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "new-password");
        }

        admin.PasswordHash = passwordHasher.Hash(request.NewPassword);

        audit.Record("admin.password.changed", admin.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Confirms a TOTP enrolment.
    /// </summary>
    /// <remarks>
    /// The secret is only stored once a code generated from it verifies — an
    /// operator who scans the QR but never confirms must not end up locked out
    /// by a second factor they never finished setting up.
    /// </remarks>
    public async Task<UseCaseResult> ConfirmTwoFactorAsync(
        Guid adminId,
        TwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await repository.FindAdminUserAsync(adminId, cancellationToken);
        if (admin is null)
        {
            return UseCaseResult.Failure(UseCaseError.Unauthorized);
        }

        var secret = request.Secret ?? admin.TwoFactorSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "secret");
        }

        if (!Totp.Verify(secret, request.Code, clock.UtcNow))
        {
            return UseCaseResult.Failure(UseCaseError.Forbidden, "code");
        }

        admin.TwoFactorSecret = secret;
        admin.TwoFactorEnabled = true;

        audit.Record("admin.2fa.enabled", admin.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Shortest new operator password this will accept.
    /// </summary>
    /// <remarks>
    /// Three layers disagreed about this: eight here, twelve in the request
    /// validator, ten in the panel's form. The validator is the one that ran
    /// first, so the effective rule was twelve and the other two were a form
    /// promising something it could not deliver. Twelve everywhere now, and
    /// the number lives beside the check that uses it.
    /// </remarks>
    public const int MinimumPasswordLength = 12;

    internal static string HashKey(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));

    private static string StatusMessage(OrderStatus status, string number) => status switch
    {
        OrderStatus.Processing => $"سفارش {number} در حال آماده‌سازی است.",
        OrderStatus.Packed => $"سفارش {number} بسته‌بندی شد.",
        OrderStatus.Shipped => $"سفارش {number} ارسال شد.",
        OrderStatus.Delivered => $"سفارش {number} تحویل داده شد.",
        OrderStatus.Cancelled => $"سفارش {number} لغو شد.",
        OrderStatus.Returned => $"سفارش {number} مرجوع شد.",
        _ => $"وضعیت سفارش {number} به‌روزرسانی شد.",
    };

    /// <summary>Serialises a settings value the way <see cref="SettingEntry.Value"/> expects it — JSON, quotes included.</summary>
    public static string EncodeSettingValue(string raw) => JsonSerializer.Serialize(raw);
}
