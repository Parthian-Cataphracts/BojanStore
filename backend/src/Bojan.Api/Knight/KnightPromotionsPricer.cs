using System.Text.Json;
using Bojan.Application.Common;
using Knight.StoreAgent;

namespace Bojan.Api.Knight;

/// <summary>
/// The integration-side <see cref="IPromotionsPricer"/>: it asks the installed
/// <c>advanced-promotions</c> Feature to price a basket, through the agent's
/// synchronous <see cref="IKnightFeatureClient"/>.
///
/// Best-effort by construction — the client returns <c>null</c> for a Feature
/// that is not installed, not connected, down or slow, and a malformed answer is
/// treated the same way — so a promotion never blocks or breaks a checkout. The
/// worst case is an order that simply carries no automatic discount.
/// </summary>
public sealed class KnightPromotionsPricer(
    IKnightFeatureClient client,
    ILogger<KnightPromotionsPricer> logger) : IPromotionsPricer
{
    public async Task<long> EvaluateAsync(
        IReadOnlyList<PromotionBasketLine> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        try
        {
            var payload = new
            {
                lines = lines.Select(l => new { productId = l.ProductId, quantity = l.Quantity, unitPrice = l.UnitPrice }),
            };

            using var doc = await client.CallAsync(
                "advanced-promotions", "POST", "/api/v1/public/evaluate", payload, cancellationToken);

            if (doc is null || !doc.RootElement.TryGetProperty("discount", out var discount))
            {
                return 0;
            }

            // The engine works in the same integer minor units the store sends,
            // so the discount comes back whole; clamp negatives away defensively.
            var value = discount.ValueKind == JsonValueKind.Number ? discount.GetDouble() : 0;
            return value > 0 ? (long)Math.Round(value) : 0;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Pricing the basket with advanced-promotions failed; proceeding with no promotion.");
            return 0;
        }
    }
}
