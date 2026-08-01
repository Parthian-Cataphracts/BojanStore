using FluentValidation;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> for whatever body an
/// endpoint bound, before the handler sees it.
/// </summary>
/// <remarks>
/// <para>
/// The storefront endpoints each take an <c>IValidator&lt;T&gt;</c> parameter
/// and call it by hand, which works because each was written that way. Not one
/// of the panel's twenty-three writes did, so every bound length in the
/// database — a settings value at 8000 characters, a title at 200 — was
/// enforced only by the database, and exceeding one produced a 500 instead of
/// the field error the form can point at.
/// </para>
/// <para>
/// Applied to the group rather than per endpoint, so a write added later is
/// covered by existing, and an endpoint whose body has no validator is
/// unaffected. Registering a validator is the whole opt-in.
/// </para>
/// </remarks>
public sealed class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            // The non-generic entry point, because the argument's type is only
            // known at run time.
            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                // The same shape the storefront's hand-rolled calls produce, so
                // both halves of the API report a bad field identically.
                return Results.ValidationProblem(result.ToDictionary());
            }
        }

        return await next(context);
    }
}
