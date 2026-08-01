namespace Bojan.Application.Common;

/// <summary>
/// Commits work, and runs a block of it atomically.
/// </summary>
/// <remarks>
/// The transaction port exists for exactly one caller that cannot do without
/// it: order placement re-prices, re-checks stock, reserves it and records a
/// coupon redemption, and a partial application of that is the worst failure
/// mode the system has (<c>BACKEND.md</c> Phase 4). Everything else saves
/// normally.
/// </remarks>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction,
    /// committing if it returns and rolling back if it throws. Nested calls
    /// join the outer transaction rather than opening a second one.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}

/// <summary>Who is making this request, resolved from the credential rather than from anything in the body.</summary>
public interface ICurrentUser
{
    /// <summary>The signed-in customer, or null on a public endpoint.</summary>
    Guid? CustomerId { get; }

    /// <summary>The signed-in operator, or null outside the panel's endpoints.</summary>
    Guid? AdminId { get; }

    /// <summary>Operator's role, lowercase as the panel writes it — <c>owner</c>, <c>product</c>, <c>sales</c>, <c>support</c>.</summary>
    string? AdminRole { get; }

    /// <summary>Caller address, for the audit trail. Null when it cannot be determined.</summary>
    string? Ip { get; }
}

/// <summary>
/// Records an operator action.
/// </summary>
/// <remarks>
/// <c>BACKEND.md</c> Phase 7: "Every write here goes in an audit log." The
/// implementation adds the row to the same change tracker as the write it
/// describes, so one <see cref="IUnitOfWork.SaveChangesAsync"/> commits both
/// or neither.
/// </remarks>
public interface IAuditLog
{
    void Record(string action, string target);
}

/// <summary>
/// Somewhere to put an uploaded file.
/// </summary>
/// <remarks>
/// <c>BACKEND.md</c> Phase 8: "Uploads are one shared decision, not six." This
/// is that decision, made once: through-the-API rather than direct-to-storage
/// with a signed URL. Product images, avatars, return photos and B2B
/// attachments all come through here, so swapping local disk for S3-compatible
/// object storage later is one implementation, not six call-site changes.
/// </remarks>
public interface IFileStorage
{
    /// <summary>Stores the stream and returns the URL the frontend should render.</summary>
    Task<string> SaveAsync(
        string folder,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string url, CancellationToken cancellationToken);
}

/// <summary>The gateway's answer to "start a payment".</summary>
public sealed record PaymentSession(string PaymentUrl, string Reference);

/// <summary>
/// Starts and verifies a payment.
/// </summary>
/// <remarks>
/// Phase 8's gateway, behind a port so the money path does not depend on which
/// Iranian PSP is wired up. The Phase 1-style stub implementation returns a
/// local callback URL, which is enough for the checkout redirect the frontend
/// already performs when <c>paymentUrl</c> is present.
/// </remarks>
public interface IPaymentGateway
{
    Task<PaymentSession> StartAsync(string orderNumber, long amountToman, CancellationToken cancellationToken);

    /// <summary>True when the gateway confirms the reference was actually paid.</summary>
    Task<bool> VerifyAsync(string reference, long amountToman, CancellationToken cancellationToken);
}

/// <summary>
/// Delivers a notification on whichever channel it was composed for.
/// </summary>
/// <remarks>
/// In-app notifications are rows this API owns; email, SMS and push leave the
/// system. Keeping all four behind one port means the composer screen's write
/// does not branch on channel.
/// </remarks>
public interface INotificationDispatcher
{
    Task DispatchAsync(Guid notificationCampaignId, CancellationToken cancellationToken);
}
