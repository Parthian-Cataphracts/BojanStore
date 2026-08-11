using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Customers;

namespace Bojan.Application.Accounts;

/// <summary>
/// The loyalty club, as the owner configures it and the storefront reads it.
/// </summary>
/// <remarks>
/// The club was a page with nothing behind it: three tiers, a permanent
/// discount and unlimited free delivery, all advertised, none applied, and
/// <c>AddLoyaltyPoints</c> called only by the seeder. This is what makes the
/// page true — the same tiers price the checkout, award the points and appear on
/// the member's own screen.
/// </remarks>
public sealed class LoyaltyService(ILoyaltyStore store, IAuditLog audit, IUnitOfWork unitOfWork)
{
    /// <summary>
    /// How many rungs the club may have.
    /// </summary>
    /// <remarks>
    /// A ladder nobody can hold in their head is a ladder nobody aims at. Six is
    /// twice what the design drew and well past what any shop this size needs.
    /// </remarks>
    private const int MaxTiers = 6;

    /// <summary>The most a shop may ask for a single point, so the club cannot be made unreachable by typo.</summary>
    private const int MaxTomanPerPoint = 10_000_000;

    public Task<LoyaltyProgrammeDto> GetAsync(CancellationToken cancellationToken) =>
        store.GetAsync(cancellationToken);

    public async Task<UseCaseResult> SaveAsync(SaveLoyaltyRequest request, CancellationToken cancellationToken)
    {
        if (request.TomanPerPoint is < 0 or > MaxTomanPerPoint)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "tomanPerPoint");
        }

        var tiers = request.Tiers ?? [];

        if (tiers.Count > MaxTiers)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "tiers");
        }

        foreach (var tier in tiers)
        {
            if (tier.Name.Trim().Length is 0 or > 80)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "name");
            }

            if (tier.MinimumPoints < 0)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "minimumPoints");
            }

            // The ceiling is in the domain as well, because a row written
            // straight into the database must not sell the shop's stock at a
            // loss either. This is the half an operator gets told about.
            if (tier.DiscountPercent is < 0 or > Loyalty.MaxDiscountPercent)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "discountPercent");
            }
        }

        var ordered = tiers.OrderBy(tier => tier.MinimumPoints).ToList();

        // Two rungs at one figure make "which tier do I hold" depend on row
        // order — a member would see one tier on their account screen and be
        // charged for another at the till.
        if (ordered.Select(tier => tier.MinimumPoints).Distinct().Count() != ordered.Count)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "duplicate-points");
        }

        // Same rule as the B2B volume ladder, for the same reason: a rung that
        // gives less than the one below it means spending more to be worse off,
        // which is a row typed on the wrong line rather than an offer.
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].DiscountPercent < ordered[index - 1].DiscountPercent)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "not-increasing");
            }
        }

        await store.SaveAsync(
            request.TomanPerPoint,
            [.. ordered.Select((tier, index) => tier with { Name = tier.Name.Trim(), SortOrder = index })],
            cancellationToken);

        audit.Record("loyalty.saved", $"{ordered.Count} tiers");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult.Success();
    }
}

/// <summary>Where the club's configuration lives.</summary>
public interface ILoyaltyStore
{
    Task<LoyaltyProgrammeDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the whole ladder, like the shipping and volume-tier screens.</summary>
    Task SaveAsync(int tomanPerPoint, IReadOnlyList<LoyaltyTierDto> tiers, CancellationToken cancellationToken);
}
