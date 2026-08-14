using Bojan.Application.Payments;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// <see cref="IPaymentDefaults"/> over the bound <see cref="PaymentOptions"/>.
/// </summary>
/// <remarks>
/// The same value <see cref="SandboxPaymentGateway"/> falls back to when the
/// panel has no callback stored, read from the one place it is configured so
/// the two cannot disagree about where a sandbox payment comes back to.
/// </remarks>
public sealed class PaymentDefaults(IOptions<PaymentOptions> options) : IPaymentDefaults
{
    public string ReturnUrl => options.Value.ReturnUrl;
}
