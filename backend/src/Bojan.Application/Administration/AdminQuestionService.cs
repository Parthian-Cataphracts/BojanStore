using Bojan.Application.Common;
using Bojan.Domain.Reviews;

namespace Bojan.Application.Administration;

/// <summary>
/// The question queue's writes — «پرسش‌ها» in the panel.
/// </summary>
/// <remarks>
/// <para>
/// A question arrives <see cref="ModerationStatus.Pending"/> and the storefront
/// shows only published ones, which the catalogue query already enforced. What
/// was missing was anything at all on this side: <c>ProductQuestion.Answer</c>
/// had no callers, there was no admin query, no endpoint and no screen. So a
/// shopper asked, the form thanked them, the row was written, and nobody could
/// ever see it or reply to it. The whole feature was write-only.
/// </para>
/// <para>
/// Answering is what publishes. That is the domain's own rule — see
/// <c>ProductQuestion.Answer</c>, which sets the reply and the status in one
/// step — and it is the right one: a question on a product page with no answer
/// under it is a shop advertising that it does not respond. The separate status
/// write exists for the other direction only, so an operator can reject
/// something abusive or off-topic without having to write a reply to it first.
/// </para>
/// </remarks>
public sealed class AdminQuestionService(
    IAdminRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IDateTimeProvider clock)
{
    /// <summary>The longest reply the panel will store.</summary>
    /// <remarks>
    /// The same ceiling the column carries. Refused here rather than left to
    /// the database, which answers an over-long value with a driver error the
    /// operator cannot act on instead of a field they can shorten.
    /// </remarks>
    private const int MaxAnswerLength = 2000;

    /// <summary>
    /// Writes the reply and publishes the question in one step.
    /// </summary>
    /// <param name="adminId">
    /// The operator answering. Their display name is what appears beside the
    /// reply on the product page — resolved here rather than taken from the
    /// request, so the shop cannot be made to answer in somebody else's name.
    /// </param>
    public async Task<UseCaseResult<string>> AnswerAsync(
        QuestionAnswerRequest request,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        var body = request.Body?.Trim() ?? string.Empty;
        if (body.Length is 0 or > MaxAnswerLength)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "body");
        }

        if (await repository.FindAdminUserAsync(adminId, cancellationToken) is not { } admin)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Unauthorized);
        }

        if (await repository.FindProductQuestionAsync(id, cancellationToken) is not { } question)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        question.Answer(admin.Name, body, clock.UtcNow);

        audit.Record("question.answered", question.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(question.Id.ToString());
    }

    /// <summary>
    /// Moves a question between moderation states without answering it.
    /// </summary>
    /// <remarks>
    /// Publishing is refused here rather than silently accepted. The storefront
    /// prints the reply under the question, so a published one with nothing to
    /// print is a shop showing a customer's question and no response — which is
    /// worse than not showing it at all. Answering is the way to publish, and
    /// it is one call away.
    /// </remarks>
    public async Task<UseCaseResult<string>> SetStatusAsync(
        QuestionModerationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        var status = request.Status switch
        {
            "pending" => ModerationStatus.Pending,
            "rejected" => ModerationStatus.Rejected,
            _ => (ModerationStatus?)null,
        };

        if (status is not { } target)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
        }

        if (await repository.FindProductQuestionAsync(id, cancellationToken) is not { } question)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        question.Status = target;

        audit.Record("question.status", $"{question.Id}:{request.Status}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(question.Id.ToString());
    }

    /// <remarks>
    /// A real delete, as a review's is. Rejecting hides a question from the
    /// product page and keeps it in the queue; this is for the ones that should
    /// not be in the queue either.
    /// </remarks>
    public async Task<UseCaseResult<string>> DeleteAsync(
        string idValue,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idValue, out var id))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "id");
        }

        if (await repository.FindProductQuestionAsync(id, cancellationToken) is not { } question)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        repository.RemoveProductQuestion(question);

        audit.Record("question.deleted", question.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(question.Id.ToString());
    }
}
