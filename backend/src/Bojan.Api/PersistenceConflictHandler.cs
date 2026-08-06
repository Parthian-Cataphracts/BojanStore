using Bojan.Application.Common;
using Bojan.Api.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api;

/// <summary>
/// Turns a write the database refused into the refusal the API already has a
/// word for.
/// </summary>
/// <remarks>
/// <para>
/// Ten unique indexes guard this schema — the customer's phone, a product's
/// slug, an order's number and idempotency key, a coupon's code, a role's grant
/// on a section, a setting's key. Every writer checks first and then inserts,
/// and nothing anywhere caught the violation when two requests passed that
/// check at the same moment: <c>DbUpdateException</c> did not appear once in
/// the whole backend. So two shoppers signing in from the same new phone number
/// at once, an operator typing a slug another product already has, or a
/// permission grid posting the same cell twice all produced a 500.
/// </para>
/// <para>
/// A 500 is the wrong answer twice over. It tells the caller the server broke
/// when what happened is that the request lost a race or duplicated a value,
/// and it is an oracle: a 500 where a 2xx would otherwise be says the value is
/// already taken, which is the enumeration signal the sign-up path is careful
/// not to give.
/// </para>
/// <para>
/// Mapped here rather than in each service because the answer is the same
/// wherever it comes from, and because a service that forgets is exactly how
/// this happened the first time. Services that can give a better field-level
/// message still check first and still do — this is the floor, not a
/// replacement.
/// </para>
/// </remarks>
internal sealed class PersistenceConflictHandler(ILogger<PersistenceConflictHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException)
        {
            return false;
        }

        // The detail names constraints and columns, so it stays in the log
        // rather than going out on the wire.
        logger.LogWarning(
            exception,
            "A write to {Path} was refused by the database and answered as a conflict.",
            httpContext.Request.Path);

        await ApiResults
            .Problem(UseCaseError.Conflict, "already-exists")
            .ExecuteAsync(httpContext);

        return true;
    }
}
