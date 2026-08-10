using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bojan.Application.Accounts;
using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Application.Orders;
using Bojan.Application.Contracts;
using Bojan.Application.Support;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Inventory;
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
    IDateTimeProvider clock,
    IBackupArchiver archiver,
    IMailboxService mailbox,
    ICustomerMailer mailer,
    EmailTemplates templates,
    AccountService accounts)
{
    /// <summary>
    /// Answers a customer's email from the support mailbox.
    /// </summary>
    /// <remarks>
    /// Here rather than in the endpoint so the audit entry is written the way
    /// every other operator action's is. Mail leaving under the shop's own
    /// address is exactly what an audit trail is for: who answered a customer,
    /// and when.
    ///
    /// Only a successful send is recorded. A failed one is the mail server
    /// refusing, not something an operator did, and an audit trail full of
    /// attempts that never left is harder to read than one that is not.
    /// </remarks>
    public async Task<MailResult> ReplyToMailAsync(MailSendRequest request, CancellationToken cancellationToken)
    {
        var result = await mailbox.SendAsync(request, cancellationToken);
        if (!result.Ok)
        {
            return result;
        }

        audit.Record("mailbox.replied", string.Join(", ", request.To));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Decides a card-to-card top-up — the review queue behind
    /// <c>POST /admin/wallet/topups/decide</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The operator is confirming that money arrived in the store's account,
    /// against a bank statement. Approving credits spendable balance that buys
    /// real goods, so it is the one admin write whose mistake costs the store
    /// cash rather than data — hence the audit entry naming the operator, and
    /// hence the decision being one-way.
    /// </para>
    /// <para>
    /// The crediting itself is <see cref="AccountService.DecideAsync"/>, shared
    /// with the gateway callback. Two paths that both put money in a wallet
    /// must not be two implementations of putting money in a wallet.
    /// </para>
    /// <para>
    /// Wrapped in a transaction because the "already-decided" check below is the
    /// only thing standing between a double-clicked approve button — or two
    /// operators working the queue at once — and a wallet credited twice for one
    /// transfer. <see cref="IAdminRepository.FindWalletTopUpAsync"/> locks the row
    /// before reading it, and a lock outside a transaction is released too early
    /// to matter.
    /// </para>
    /// </remarks>
    public Task<UseCaseResult> DecideWalletTopUpAsync(
        Guid adminId,
        Guid topUpId,
        bool approve,
        string? note,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var topUp = await repository.FindWalletTopUpAsync(topUpId, token);
                if (topUp is null)
                {
                    return UseCaseResult.Failure(UseCaseError.NotFound);
                }

                if (topUp.Status is not WalletTopUpStatus.Pending)
                {
                    return UseCaseResult.Failure(UseCaseError.Invalid, "already-decided");
                }

                // A gateway top-up is settled by the gateway's own verification.
                // Letting an operator approve one by hand would be a way to
                // credit a wallet for a payment that was never taken, using a
                // screen meant for the transfers where a human check is the only
                // check there is.
                if (topUp.Method is not WalletTopUpMethod.Manual)
                {
                    return UseCaseResult.Failure(UseCaseError.Invalid, "not-manual");
                }

                await accounts.DecideAsync(topUp, approve, adminId, note?.Trim(), token);

                audit.Record(approve ? "wallet.topup.approved" : "wallet.topup.rejected", topUp.Id.ToString());
                await unitOfWork.SaveChangesAsync(token);

                // The customer sent money by bank transfer and has been waiting
                // on a person to look at it. Either answer is worth an email —
                // and a rejection especially, because without a written reason
                // it becomes a phone call.
                var owner = await repository.FindCustomerAsync(topUp.CustomerId, token);
                await mailer.SendAsync(
                    owner?.Email,
                    approve
                        ? templates.WalletToppedUp(
                            topUp.Amount.Amount,
                            owner?.WalletBalance.Amount ?? topUp.Amount.Amount,
                            clock.UtcNow)
                        : templates.WalletRejected(topUp.Amount.Amount, clock.UtcNow, note),
                    token);

                return UseCaseResult.Success();
            },
            cancellationToken);

    /// <summary>
    /// Moves an order on and tells the customer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BACKEND.md</c> Phase 7 leaves open whether the transition or a
    /// separate job sends the notification. The in-app one is done here, in the
    /// same transaction: an order that moved to <c>shipped</c> without the
    /// customer being told is a support ticket, and a queue that can drop the
    /// message is a worse trade than a transaction that is one row longer.
    /// </para>
    /// <para>
    /// The email is not. It goes out after the transaction has committed,
    /// because it is a call to a mail server and holding the order's row lock
    /// across it would let one slow or unreachable SMTP host block every other
    /// operator working that order. The trade is the other way round from the
    /// notification's: a dropped email is a resend, while a lock held on a
    /// network round trip is a queue behind it.
    /// </para>
    /// <para>
    /// Under a transaction and a locked row, like the cancellation beside it.
    /// This was the one write on the order aggregate that had neither: reading
    /// the status, deciding from it whether the move is legal, and then writing
    /// it is a read-modify-write, and two operators doing it at once both
    /// passed the check, both appended a timeline event and both notified the
    /// customer. What survived was a status that disagreed with its own
    /// history — an order recorded as shipped and delivered at the same
    /// instant, left in whichever state committed last.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult> UpdateOrderStatusAsync(
        Guid actorId,
        OrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        if (WireFormat.ParseOrderStatus(request.Status) is not { } status)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "status");
        }

        // Cancelling is not a status change. It restores stock and refunds the
        // wallet less any penalty, none of which happens here — so allowing it
        // through this endpoint would cancel the order and leave the customer's
        // money and the shop's stock where they were. The panel routes it to
        // its own control; this is that rule enforced where it holds even for a
        // request that never went near the panel.
        if (status is OrderStatus.Cancelled)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "use-cancel-endpoint");
        }

        // Captured so the mail below can be addressed once the transaction has
        // committed. Assigned only on the success path, which is what makes
        // "committed" and "not null" the same condition.
        Order? moved = null;

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var order = await repository.FindOrderForUpdateAsync(id, token);
                if (order is null)
                {
                    return UseCaseResult.Failure(UseCaseError.NotFound);
                }

                try
                {
                    repository.AddOrderTimelineEvent(
                        order.TransitionTo(status, request.TrackingCode, actorId, request.Note));
                }
                catch (OrderNotPaidException)
                {
                    // Its own key. "This order has not been paid for" and "this
                    // order is already delivered" are different problems with
                    // different fixes, and one message for both sends the
                    // operator looking in the wrong place.
                    return UseCaseResult.Failure(UseCaseError.Conflict, "order-not-paid");
                }
                catch (InvalidOperationException)
                {
                    // Terminal, or a move that is not forward. Either way the
                    // operator asked for something the domain forbids, which is
                    // not a server fault.
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
                    // The copy lives in OrderNotices rather than inline here, so
                    // the wording can be reviewed as writing. WithLink rather
                    // than setting Href directly: an operator-reachable
                    // destination is validated against the domain, and this one
                    // goes through the same door as the rest.
                    Body = OrderNotices.StatusChanged(status, order.Number),
                    CreatedAtUtc = clock.UtcNow,
                }.WithLink($"/account/orders/{order.Id}"));

                audit.Record("order.status.changed", order.Number);
                await unitOfWork.SaveChangesAsync(token);

                moved = order;
                return UseCaseResult.Success();
            },
            cancellationToken);

        if (!result.IsSuccess || moved is null)
        {
            return result;
        }

        // Two of the seven transitions are news to a customer. "Processing" and
        // "packed" are the shop talking to itself; shipping gives them a
        // tracking code and delivery gives them an invoice number, and those are
        // the two they act on.
        var buyer = await repository.FindCustomerAsync(moved.CustomerId, cancellationToken);

        if (status is OrderStatus.Shipped)
        {
            await mailer.SendAsync(
                buyer?.Email,
                templates.OrderShipped(
                    moved.Number,
                    moved.Id,
                    moved.ShippingMethodName,
                    moved.TrackingCode,
                    moved.ShippingAddressSnapshot),
                cancellationToken);
        }
        else if (status is OrderStatus.Delivered && moved.InvoiceNumber is { Length: > 0 } invoiceNumber)
        {
            await mailer.SendAsync(
                buyer?.Email,
                templates.OrderDelivered(
                    moved.Number,
                    moved.Id,
                    invoiceNumber,
                    moved.DeliveredAtUtc ?? clock.UtcNow,
                    moved.Total.Amount),
                cancellationToken);
        }

        return UseCaseResult.Success();
    }

    /// <summary>
    /// Records that an order's money arrived — the operator's half of the
    /// payment state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Adapted from Phonix, whose orders start at <c>PendingApproval</c> and are
    /// settled by an operator confirming a receipt against a bank statement.
    /// That is the right model here for the same reason it is there: the only
    /// <see cref="IPaymentGateway"/> available is a sandbox that approves
    /// everything, so a gateway's word is not evidence and a person's is.
    /// </para>
    /// <para>
    /// Under the order's row lock inside a transaction, because
    /// <see cref="Order.MarkPaid"/>'s idempotence is only trustworthy when the
    /// status it reads was read under that lock — the same arrangement the
    /// wallet top-up decision uses, and for the same reason: two operators
    /// working the queue at once must not settle one order twice.
    /// </para>
    /// </remarks>
    public Task<UseCaseResult> SettleOrderPaymentAsync(
        Guid actorId,
        OrderPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return Task.FromResult(UseCaseResult.Failure(UseCaseError.Invalid, "id"));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var order = await repository.FindOrderForUpdateAsync(id, token);
                if (order is null)
                {
                    return UseCaseResult.Failure(UseCaseError.NotFound);
                }

                var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();

                var settled = request.Paid
                    ? order.MarkPaid(clock.UtcNow, reference, actorId)
                    : order.MarkPaymentFailed(reference);

                if (!settled)
                {
                    // Already decided. Reported rather than repeated, so a
                    // double-clicked button is harmless instead of confusing.
                    return UseCaseResult.Failure(UseCaseError.Conflict, "already-settled");
                }

                if (request.Paid)
                {
                    repository.AddCustomerNotification(new CustomerNotification
                    {
                        CustomerId = order.CustomerId,
                        Kind = NotificationKind.Order,
                        Title = $"سفارش {order.Number}",
                        Body = OrderNotices.PaymentConfirmed(order.Number),
                        CreatedAtUtc = clock.UtcNow,
                    }.WithLink($"/account/orders/{order.Id}"));
                }

                audit.Record(request.Paid ? "order.payment.settled" : "order.payment.failed", order.Number);
                await unitOfWork.SaveChangesAsync(token);
                return UseCaseResult.Success();
            },
            cancellationToken);
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
                CreatedAtUtc = clock.UtcNow,
            }.WithLink($"/account/support/{ticket.Id}"));
        }

        audit.Record("support.replied", ticket.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (ticket.CustomerId is { } notifyId)
        {
            var owner = await repository.FindCustomerAsync(notifyId, cancellationToken);
            await mailer.SendAsync(
                owner?.Email,
                templates.TicketReplied(ticket.Id, ticket.Subject),
                cancellationToken);
        }

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
    /// Queues a broadcast. Delivery is <see cref="INotificationDispatcher"/>'s.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to fan an immediate in-app broadcast out inline, writing one
    /// row per customer inside the operator's request, and leave everything else
    /// to the dispatcher — which nothing called, so "everything else" meant
    /// scheduled campaigns and every SMS broadcast were stored and never sent.
    /// </para>
    /// <para>
    /// With <c>NotificationWorker</c> draining the queue there is one delivery
    /// path for every channel and every schedule. That also takes the fan-out
    /// out of the request: an audience of a hundred thousand was a hundred
    /// thousand inserts in one transaction the operator waited on.
    /// </para>
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

        // A channel with nothing behind it is refused here rather than queued
        // and quietly dropped at dispatch. It used to be accepted: the campaign
        // was stored, the panel reported it sent, the dispatcher logged that
        // nobody was reached, and the only place that showed was the server log.
        // An operator being told the broadcast failed is worth more than a row
        // in a table nobody reads.
        //
        // Email came off this list when the dispatcher grew a branch for it and
        // the shop grew an account to send from. Push is still on it: there is
        // no provider, and a tile that queues nothing is worse than one that
        // says so.
        if (channel is NotificationChannel.Push)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "channel-unavailable");
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

        audit.Record("notification.queued", request.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return campaign.Id.ToString();
    }

    /// <summary>
    /// Sends one in-app notification to one customer.
    /// </summary>
    /// <remarks>
    /// The panel could only address a whole audience group before this, so
    /// telling one shopper something about their own order meant either a
    /// broadcast to everybody or a phone call. It writes the row directly
    /// rather than going through <see cref="QueueBroadcastAsync"/> — there is
    /// nothing to fan out and nothing to schedule, and a campaign row for a
    /// single message would make the campaign reports read as if the shop ran
    /// hundreds of them.
    /// </remarks>
    public async Task<UseCaseResult> NotifyCustomerAsync(
        CustomerNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.CustomerId, out var customerId))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "customerId");
        }

        // Checked rather than assumed: this is the one notification path where
        // the recipient arrives in the request body instead of being derived
        // from a row the operator was already looking at.
        if (await repository.FindCustomerAsync(customerId, cancellationToken) is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        // Refused here rather than thrown from the domain, so an operator who
        // pastes a full URL is told what is wrong with it.
        if (!string.IsNullOrWhiteSpace(request.Link)
            && !CustomerNotification.IsInternalPath(request.Link.Trim()))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "link");
        }

        repository.AddCustomerNotification(new CustomerNotification
        {
            CustomerId = customerId,
            Kind = NotificationKind.Account,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            CreatedAtUtc = clock.UtcNow,
        }.WithLink(request.Link));

        audit.Record("notification.sent", request.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Queues an export. Returns as soon as the row exists —
    /// <c>BACKEND.md</c> Phase 7: "it is not a synchronous download."
    /// </summary>
    /// <param name="actorRole">
    /// The caller's role, checked against <see cref="ReportCatalogue"/>. The
    /// report name arrives in the body, so the route's own policy cannot gate
    /// it — and without this the queue was a way to obtain a report whose read
    /// endpoint refuses the caller.
    /// </param>
    public async Task<UseCaseResult<string>> QueueReportExportAsync(
        Guid actorId,
        string? actorRole,
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        if (!ReportCatalogue.IsKnown(request.Report))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "report");
        }

        if (!ReportCatalogue.CanExport(request.Report, actorRole))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Forbidden, "report");
        }

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

    /// <summary>
    /// The kinds screen 156 offers. Anything else is a crafted body.
    /// </summary>
    /// <remarks>
    /// Checked here rather than left to the worker: a job queued for a kind
    /// nothing can produce would sit in the list and then fail, which reads as
    /// the shop's backups being broken rather than as a bad request.
    /// </remarks>
    private static readonly string[] BackupKinds = ["database", "media", "full"];

    /// <summary>
    /// Screen 156's "پشتیبان‌گیری" — queues the job and returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to write a small JSON file naming the job — its id, its kind,
    /// who asked and when — mark the row <c>Completed</c> and hand the panel a
    /// download link. No database was dumped and no uploaded file was archived.
    /// An operator who pressed the button got every signal that they were
    /// protected and none of the protection.
    /// </para>
    /// <para>
    /// The archive is built by <c>BackupWorker</c> instead. A real dump takes
    /// minutes, and a request holding a connection open that long times out at
    /// the proxy while the work carries on unwatched — so the row is the queue,
    /// exactly as it is for report exports, and the panel polls it.
    /// </para>
    /// </remarks>
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

        if (!BackupKinds.Contains(request.Kind, StringComparer.Ordinal))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "kind");
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

    public async Task<IReadOnlyList<BackupJobDto>> ListBackupJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = await repository.ListBackupJobsAsync(cancellationToken);
        return [.. jobs.Select(ToDto)];
    }

    /// <summary>
    /// The archive as a stream and a filename for it, or null when the job has
    /// none yet (still processing, or failed) or the caller names an id that
    /// does not exist. Reads the file itself rather than handing back a
    /// location — see <see cref="IBackupArchiver"/> for why this content is
    /// never reachable by a URL.
    /// </summary>
    /// <remarks>
    /// A stream, not a byte array. A backup archive is now a database dump and
    /// the whole media tree rather than a few hundred bytes of JSON, and
    /// reading one into memory to hand it to the response would put the size of
    /// the shop on this process's heap once per download.
    /// </remarks>
    public async Task<(Stream Content, string FileName)?> GetBackupFileAsync(
        Guid jobId, CancellationToken cancellationToken)
    {
        var job = await repository.FindBackupJobAsync(jobId, cancellationToken);
        if (job?.ArchiveReference is not { } reference)
        {
            return null;
        }

        var content = await archiver.OpenReadStreamAsync(reference, cancellationToken);
        return content is null ? null : (content, reference);
    }

    /// <summary>
    /// Screen 140's download, once the background export worker has moved
    /// the row to <see cref="JobStatus.Completed"/>. Same shape and the same
    /// reason as <see cref="GetBackupFileAsync"/> — <c>null</c> covers "still
    /// queued", "failed" and "no such id" alike, which is all the caller
    /// needs to know to answer 404.
    /// </summary>
    /// <param name="requesterId">
    /// The operator asking. An export is returned only to whoever queued it:
    /// the route is open to every role, so without this an operator holding an
    /// id could read a colleague's report — including one their own role may
    /// not run. Answering not-found rather than forbidden keeps an id that
    /// exists indistinguishable from one that does not, as everywhere else
    /// ownership is checked here.
    /// </param>
    public async Task<(byte[] Content, string FileName)?> GetReportExportFileAsync(
        Guid exportId, Guid requesterId, CancellationToken cancellationToken)
    {
        var export = await repository.FindReportExportAsync(exportId, cancellationToken);
        if (export is null || export.RequestedById != requesterId || export.FileUrl is not { } reference)
        {
            return null;
        }

        var content = await archiver.OpenReadAsync(reference, cancellationToken);
        return content is null ? null : (content, $"{export.Report}.csv");
    }

    public async Task<IReadOnlyList<RolePermissionDto>> ListRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var grants = await repository.ListRolePermissionsAsync(cancellationToken);
        return [.. grants.Select(g => new RolePermissionDto(g.Role, g.Section))];
    }

    /// <summary>
    /// Screen 146's save button. <c>owner</c> is refused outright — it is
    /// never stored, and a body that tries to grant or revoke it is a bug in
    /// the caller, not a request to honour partially.
    /// </summary>
    public async Task<UseCaseResult> SaveRolePermissionsAsync(
        Guid actorId,
        IReadOnlyList<RoleGrantRequest> grants,
        CancellationToken cancellationToken)
    {
        var roles = new[] { "product", "sales", "support" };

        // The grid is three roles by ten sections, so thirty is every box on
        // it. Without a ceiling the body was unbounded: a list of a million
        // entries was read into memory, projected, and handed to the database
        // as a million inserts, and the answer to that was a 500 rather than a
        // refusal.
        if (grants.Count > roles.Length * PanelSection.All.Count)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "grants");
        }

        // The section has to be one of the known keys, not any string the
        // caller likes. It used to be stored verbatim, so a body naming a
        // section that does not exist wrote a row that granted nothing and
        // could never be revoked from the grid — and the grid posted its own
        // Persian labels, which made every permission depend on a display
        // string surviving unchanged.
        if (grants.Any(g => !roles.Contains(g.Role, StringComparer.OrdinalIgnoreCase)
            || !PanelSection.IsKnown(g.Section)))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "grants");
        }

        // Distinct because the stored thing is a set: the same box named twice
        // in one body is one grant, not two. The unique index would have said
        // so as a conflict, which is the right answer to a *renamed* coupon and
        // the wrong one to a grid that repeated itself — nothing about the
        // request is ambiguous.
        var granted = grants
            .Where(g => g.Granted)
            .Select(g => (Role: g.Role.ToLowerInvariant(), g.Section))
            .Distinct()
            .Select(g => new RolePermission { Role = g.Role, Section = g.Section })
            .ToList();

        await repository.ReplaceRolePermissionsAsync(granted, cancellationToken);
        audit.Record("roles.permissions.saved", $"{granted.Count} grants");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    private static BackupJobDto ToDto(BackupJob job) => new(
        job.Id.ToString(),
        job.Kind,
        job.Status.ToString().ToLowerInvariant(),
        job.ArchiveReference is not null,
        job.SizeBytes,
        job.Error,
        job.RequestedAtUtc,
        job.CompletedAtUtc);

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

        // Every other session this operator has open stops working, including
        // the one someone else may be holding — which is usually the reason the
        // password is being changed at all. The caller's own session goes with
        // them, so the panel signs them in again on the next request.
        admin.RotateSecurityStamp();

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

    /// <summary>Serialises a settings value the way <see cref="SettingEntry.Value"/> expects it — JSON, quotes included.</summary>
    public static string EncodeSettingValue(string raw) => JsonSerializer.Serialize(raw);
}
