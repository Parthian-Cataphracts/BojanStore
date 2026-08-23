using Bojan.Application.Common;
using Bojan.Domain.Reviews;

namespace Bojan.Application.Administration;

/// <summary>
/// The review moderation queue's writes — «نظرات مشتریان» in the panel.
/// </summary>
/// <remarks>
/// <para>
/// A review arrives <see cref="ModerationStatus.Pending"/> and is invisible
/// until an operator says otherwise; that much the storefront already enforced,
/// with nothing in the panel able to say otherwise. This is that missing half.
/// </para>
/// <para>
/// Two decisions, not one. «تأیید» decides whether the review appears on its
/// product page at all, and «نمایش در صفحه اصلی» decides whether it is also one
/// of the handful quoted on the home page. Collapsing them would mean every
/// approval is a promotion to the shop's front door, which is not what
/// approving a three-star review means.
/// </para>
/// </remarks>
public sealed class AdminReviewService(
    IAdminRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit)
{
    /// <summary>
    /// Moves a review between moderation states.
    /// </summary>
    /// <remarks>
    /// Taking a review out of «تأیید شده» clears the home-page flag with it.
    /// Leaving the flag set on a rejected review would mean the shop's decision
    /// to pull it back depends on a second tick nobody was told about — and the
    /// storefront query, which requires both, would quietly disagree with a
    /// panel still showing the star lit.
    /// </remarks>
    public async Task<UseCaseResult<string>> SetStatusAsync(
        ReviewModerationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        var status = request.Status switch
        {
            "pending" => ModerationStatus.Pending,
            "published" => ModerationStatus.Published,
            "rejected" => ModerationStatus.Rejected,
            _ => (ModerationStatus?)null,
        };

        if (status is not { } target)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
        }

        if (await repository.FindProductReviewAsync(id, cancellationToken) is not { } review)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        review.Status = target;
        if (target != ModerationStatus.Published) review.IsFeaturedOnHome = false;

        audit.Record("review.status", $"{review.Id}:{request.Status}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(review.Id.ToString());
    }

    /// <summary>
    /// Ticks or unticks «نمایش در صفحه اصلی».
    /// </summary>
    /// <remarks>
    /// Featuring is refused on anything but a published review rather than
    /// silently accepted. The storefront requires both conditions, so a tick on
    /// a pending review would save, show as lit in the panel, and put nothing on
    /// the home page — an operator staring at a rail that does not contain the
    /// review they just featured, with no way to tell which half is wrong.
    /// </remarks>
    public async Task<UseCaseResult<string>> SetFeaturedAsync(
        ReviewFeatureRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        if (await repository.FindProductReviewAsync(id, cancellationToken) is not { } review)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        if (request.Featured && review.Status != ModerationStatus.Published)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
        }

        review.IsFeaturedOnHome = request.Featured;

        audit.Record("review.featured", $"{review.Id}:{request.Featured}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(review.Id.ToString());
    }

    /// <remarks>
    /// A real delete — see <see cref="IAdminRepository.RemoveProductReview"/>
    /// for why hiding it instead would lock the customer out of ever writing
    /// another review of that product.
    /// </remarks>
    public async Task<UseCaseResult<string>> DeleteAsync(
        string idValue,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idValue, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        if (await repository.FindProductReviewAsync(id, cancellationToken) is not { } review)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        repository.RemoveProductReview(review);

        audit.Record("review.deleted", review.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(review.Id.ToString());
    }
}
