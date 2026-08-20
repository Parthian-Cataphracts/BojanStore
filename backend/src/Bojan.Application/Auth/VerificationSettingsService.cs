using Bojan.Application.Common;

namespace Bojan.Application.Auth;

public sealed record SaveVerificationSettingsRequest(bool RequireEmailVerification, bool RequirePhoneVerification);

/// <summary>
/// The panel's verification settings screen — owner only, two independent
/// toggles, both off by default.
/// </summary>
/// <remarks>
/// Storage and reads only. Nothing here yet blocks sign-in or checkout on an
/// unverified email or phone — that enforcement is future scope, and turning
/// a toggle on today only changes what this screen reports back.
/// </remarks>
public sealed class VerificationSettingsService(
    IVerificationSettingsStore store,
    IAuditLog audit,
    IUnitOfWork unitOfWork)
{
    public Task<VerificationSettingsDto> GetAsync(CancellationToken cancellationToken) => store.GetAsync(cancellationToken);

    public async Task<UseCaseResult> SaveAsync(SaveVerificationSettingsRequest request, CancellationToken cancellationToken)
    {
        await store.SaveAsync(
            new VerificationSettingsDto(request.RequireEmailVerification, request.RequirePhoneVerification),
            cancellationToken);

        audit.Record(
            "verification.settings.saved",
            $"email={request.RequireEmailVerification} phone={request.RequirePhoneVerification}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult.Success();
    }
}
