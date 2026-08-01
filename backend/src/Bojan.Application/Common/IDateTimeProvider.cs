namespace Bojan.Application.Common;

/// <summary>Indirection over <see cref="DateTimeOffset.UtcNow"/> so time-dependent logic (OTP expiry, coupon validity) is testable without a real clock.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
