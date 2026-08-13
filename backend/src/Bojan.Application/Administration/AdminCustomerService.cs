using Bojan.Application.Common;
using Bojan.Domain.Customers;

namespace Bojan.Application.Administration;

/// <summary>
/// The customer record, from the panel.
/// </summary>
/// <remarks>
/// <para>
/// The panel could suspend a customer and set their password, and that was the
/// whole of it — a name typed wrongly at registration stayed wrong for good, an
/// address change had to come from the customer, and an account created by
/// mistake could not be removed. Everything on the record is editable here now,
/// and an account with no history behind it can be deleted outright.
/// </para>
/// <para>
/// Its own service rather than more of <see cref="AdminOperationsService"/>, for
/// the reason <see cref="AdminUserService"/> is: this writes the record a
/// person's orders, wallet and identity hang off, and the rules about what may
/// be changed and what may be removed belong somewhere they can be read
/// together.
/// </para>
/// </remarks>
public sealed class AdminCustomerService(
    IAdminRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit)
{
    public async Task<UseCaseResult> SaveAsync(
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        if (await repository.FindCustomerAsync(id, cancellationToken) is not { } customer)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        // Phone and code are both unique and both quoted back at people — one
        // is how the customer signs in, the other is how the shop refers to
        // them. Checked before the index refuses them, so a clash is a field
        // error rather than a 500 from a constraint.
        var phone = request.Phone?.Trim();
        if (!string.IsNullOrEmpty(phone) && phone != customer.Phone)
        {
            if (await repository.IsCustomerPhoneTakenAsync(phone, id, cancellationToken))
            {
                return UseCaseResult.Failure(UseCaseError.Conflict, "phone-taken");
            }

            customer.Phone = phone;
            // The number is the sign-in key. Changing it must not leave a
            // session open that was opened against the old one.
            customer.RotateSecurityStamp();
        }

        var code = request.Code?.Trim();
        if (!string.IsNullOrEmpty(code) && code != customer.Code)
        {
            if (await repository.IsCustomerCodeTakenAsync(code, id, cancellationToken))
            {
                return UseCaseResult.Failure(UseCaseError.Conflict, "code-taken");
            }

            customer.Code = code;
        }

        if (request.FirstName is not null) customer.FirstName = request.FirstName.Trim();
        if (request.LastName is not null) customer.LastName = request.LastName.Trim();
        if (request.City is not null) customer.City = Blank(request.City);
        if (request.NationalId is not null) customer.NationalId = Blank(request.NationalId);
        if (request.Group is not null && request.Group.Trim().Length > 0) customer.Group = request.Group.Trim();

        if (request.Email is not null)
        {
            var email = Blank(request.Email)?.ToLowerInvariant();

            if (email is not null && await repository.IsCustomerEmailTakenAsync(email, id, cancellationToken))
            {
                return UseCaseResult.Failure(UseCaseError.Conflict, "email-taken");
            }

            customer.Email = email;
        }

        if (request.BirthDate is not null)
        {
            if (request.BirthDate.Length == 0)
            {
                customer.BirthDate = null;
            }
            else if (DateOnly.TryParse(request.BirthDate, out var birthDate))
            {
                customer.BirthDate = birthDate;
            }
            else
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "birthDate");
            }
        }

        audit.Record("customer.updated", customer.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Removes a customer outright.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the account that should never have existed — a duplicate, a test, a
    /// registration somebody abandoned. What comes with it is the personal data
    /// hanging off it: addresses, wishlist, notifications, search history,
    /// sessions. Those are configured to cascade precisely because they are the
    /// person rather than the shop's records of trading with them.
    /// </para>
    /// <para>
    /// An account that has traded cannot be removed, and that is not a
    /// limitation to work around. Orders, wallet transactions and support
    /// tickets name their customer with a restricting key, because an invoice
    /// whose customer has been deleted is not a tidier invoice — it is an
    /// unreadable one, and the shop is required to keep it. Suspension is what
    /// that account gets: every door closed, every record intact.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult> DeleteAsync(string customerId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(customerId, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        if (await repository.FindCustomerAsync(id, cancellationToken) is not { } customer)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        // Asked before the database is asked, so the answer is a sentence about
        // orders rather than a foreign-key violation surfacing as a conflict.
        if (await repository.CustomerHasTradingHistoryAsync(id, cancellationToken))
        {
            return UseCaseResult.Failure(UseCaseError.Conflict, "customer-has-history");
        }

        // Recorded before the row goes, because afterwards there is no phone
        // number to record it against.
        audit.Record("customer.deleted", customer.Phone);

        repository.RemoveCustomer(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>An empty string clears an optional field rather than storing "".</summary>
    private static string? Blank(string value) => value.Trim() is { Length: > 0 } text ? text : null;
}
