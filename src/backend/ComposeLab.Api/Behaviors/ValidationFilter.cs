using FluentValidation;

namespace ComposeLab.Api.Behaviors;

/// <summary>
/// Validates a request body before the handler is resolved. Applied per endpoint rather than to a
/// route group, because a group-wide filter would try to validate endpoints that carry only route
/// parameters and silently do nothing.
/// </summary>
internal sealed class ValidationFilter<TRequest>(IEnumerable<IValidator<TRequest>> validators)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // IEnumerable, not a nullable IValidator: Microsoft DI throws on an unregistered constructor
        // dependency but hands back an empty sequence instead.
        IValidator<TRequest>? validator = validators.FirstOrDefault();

        if (validator is null)
        {
            return await next(context);
        }

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        FluentValidation.Results.ValidationResult result =
            await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (result.IsValid)
        {
            return await next(context);
        }

        Dictionary<string, string[]> errors = result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Results.ValidationProblem(errors);
    }
}
