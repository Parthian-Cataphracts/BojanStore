namespace Bojan.Application.Common;

/// <summary>
/// Why a use case refused.
/// </summary>
/// <remarks>
/// <para>
/// These are machine keys, not sentences. <c>BACKEND.md</c> section 1.4:
/// "Never return a Persian string — the frontend owns user-facing text." The
/// API turns one of these into a <c>ProblemDetails</c> whose <c>title</c> is
/// the key and whose status is chosen by
/// <c>Bojan.Api.ProblemResults.From</c>; the frontend maps it to whatever copy
/// the screen already has.
/// </para>
/// <para>
/// <see cref="NotFound"/> is deliberately used where a stricter reading would
/// say "forbidden". <c>BACKEND.md</c> Phase 3: an order belonging to someone
/// else must 404, not 403, because a 403 confirms the order exists.
/// </para>
/// </remarks>
public enum UseCaseError
{
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    /// <summary>The request was well-formed but breaks a rule — an empty basket, a coupon below its minimum spend.</summary>
    Invalid,
    /// <summary>An item is out of stock, or not enough of it is left.</summary>
    OutOfStock,
    /// <summary>A coupon that does not exist, has expired, or has been used up.</summary>
    CouponRejected,
    /// <summary>
    /// The order was placed but the gateway could not be reached to start the
    /// payment.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Invalid"/> or a fault: nothing about the
    /// request was wrong and the order is real and reserved, so the shopper has
    /// to be sent back to pay for it rather than told to place another one. The
    /// detail carries the order number for exactly that.
    /// </remarks>
    PaymentUnavailable,
    /// <summary>
    /// The shop requires a verified email or phone before checkout, and this
    /// customer has neither confirmed. <c>Detail</c> carries which one.
    /// </summary>
    VerificationRequired,
}

/// <summary>
/// A use case's answer: a value, or a reason there is not one.
/// </summary>
/// <remarks>
/// Exceptions are reserved for genuine faults here. A rejected coupon and a
/// sold-out product are ordinary outcomes of the money path — throwing for
/// them would make the happy path indistinguishable from a bug in the logs,
/// and would hand the endpoint a stack trace where it needs a status code.
/// </remarks>
public readonly record struct UseCaseResult<T>
{
    private readonly T? _value;

    private UseCaseResult(T value)
    {
        _value = value;
        Error = null;
        Detail = null;
    }

    private UseCaseResult(UseCaseError error, string? detail)
    {
        _value = default;
        Error = error;
        Detail = detail;
    }

    public UseCaseError? Error { get; }

    /// <summary>Machine-readable extra context, e.g. the slug that was out of stock. Never user-facing copy.</summary>
    public string? Detail { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"No value: the use case failed with {Error}.");

    public static UseCaseResult<T> Success(T value) => new(value);

    public static UseCaseResult<T> Failure(UseCaseError error, string? detail = null) => new(error, detail);

    public static implicit operator UseCaseResult<T>(T value) => Success(value);
}

/// <summary>The same, for a use case that has nothing to return.</summary>
public readonly record struct UseCaseResult
{
    private UseCaseResult(UseCaseError? error, string? detail)
    {
        Error = error;
        Detail = detail;
    }

    public UseCaseError? Error { get; }

    public string? Detail { get; }

    public bool IsSuccess => Error is null;

    public static UseCaseResult Success() => new(null, null);

    public static UseCaseResult Failure(UseCaseError error, string? detail = null) => new(error, detail);
}
