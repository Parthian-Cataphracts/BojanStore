using Bojan.Application.Common;
using Bojan.Application.Contracts;

namespace Bojan.Application.Administration;

/// <summary>Reads and writes the shop's shipping tiers.</summary>
public interface IShippingMethodStore
{
    Task<IReadOnlyList<AdminShippingMethodDto>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(IReadOnlyList<AdminShippingMethodDto> methods, CancellationToken cancellationToken);
}

/// <summary>
/// The panel's shipping settings screen.
/// </summary>
/// <remarks>
/// The screen it backs used to write prices into the generic settings table
/// where nothing read them, while the checkout charged from rows only the
/// seeder had ever written. This makes the two one thing.
/// </remarks>
public sealed class ShippingSettingsService(IShippingMethodStore store, IAuditLog audit)
{
    /// <summary>
    /// The ceiling on a shipping price, in Toman.
    /// </summary>
    /// <remarks>
    /// Not a commercial judgement — a hundred million Toman is not a delivery
    /// fee anyone means to charge. It is here so a mistyped figure is refused
    /// by the form rather than discovered by a shopper at checkout.
    /// </remarks>
    private const long MaximumPrice = 100_000_000;

    public Task<IReadOnlyList<AdminShippingMethodDto>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    public async Task<UseCaseResult> SaveAsync(
        SaveShippingMethodsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Methods.Count is 0 or > 20)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "methods");
        }

        foreach (var method in request.Methods)
        {
            if (method.Code.Trim().Length is 0 or > 50)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "code");
            }

            if (method.Title.Trim().Length is 0 or > 100)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "title");
            }

            if (method.Price is < 0 or > MaximumPrice)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "price");
            }

            if (method.Estimate.Trim().Length > 100)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "estimate");
            }
        }

        // Every tier switched off would leave the checkout with nothing to pick
        // and no way to place an order at all — a shop closed by a settings
        // form, with no message saying so.
        //
        // Judged on the state this save would leave behind, not on the list it
        // carries. The panel always posts all three, but a request that names
        // only one passes a check made against its own contents while every
        // tier it left out is already off — and the shop stops taking orders.
        // A rule about the shop has to be asked about the shop.
        var submitted = request.Methods
            .ToDictionary(method => method.Code.Trim(), method => method.IsActive, StringComparer.Ordinal);

        var resulting = await store.ListAsync(cancellationToken);

        if (resulting.All(method =>
            !(submitted.TryGetValue(method.Code, out var wanted) ? wanted : method.IsActive)))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "all-inactive");
        }

        await store.SaveAsync(
            [.. request.Methods.Select(method => method with
            {
                Code = method.Code.Trim(),
                Title = method.Title.Trim(),
                Estimate = method.Estimate.Trim(),
            })],
            cancellationToken);

        audit.Record("shipping.methods.saved", string.Join(", ", request.Methods.Select(m => m.Code)));

        return UseCaseResult.Success();
    }
}
