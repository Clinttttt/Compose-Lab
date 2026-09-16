using ComposeLab.Api.Behaviors;
using ComposeLab.Api.Tests.TestSupport;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace ComposeLab.Api.Tests.Behaviors;

/// <summary>
/// The filter sits in the path of every request that carries a body, so a regression here breaks the
/// API silently. The invocation counts are the point: awaiting next twice produces correct-looking
/// responses while executing every request twice.
/// </summary>
public sealed class ValidationFilterTests
{
    [Fact]
    public async Task NoValidator_InvokesNextExactlyOnce()
    {
        int calls = 0;
        ValidationFilter<Thing> filter = new([]);

        object? result = await filter.InvokeAsync(Context(new Thing("ok")), Next(() => calls++));

        calls.ShouldBe(1);
        result.ShouldBeOfType<Ok>();
    }

    [Fact]
    public async Task ValidRequest_InvokesNextExactlyOnce()
    {
        int calls = 0;
        ValidationFilter<Thing> filter = new([new ThingValidator()]);

        object? result = await filter.InvokeAsync(Context(new Thing("ok")), Next(() => calls++));

        calls.ShouldBe(1);
        result.ShouldBeOfType<Ok>();
    }

    [Fact]
    public async Task InvalidRequest_DoesNotInvokeNext_AndReturnsValidationProblem()
    {
        int calls = 0;
        ValidationFilter<Thing> filter = new([new ThingValidator()]);

        object? result = await filter.InvokeAsync(Context(new Thing("")), Next(() => calls++));

        calls.ShouldBe(0);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>()!
            .StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        HttpValidationProblemDetails details = result
            .ShouldBeAssignableTo<IValueHttpResult>()!
            .Value.ShouldBeOfType<HttpValidationProblemDetails>();

        details.Errors.ShouldContainKey(nameof(Thing.Name));
    }

    /// <summary>
    /// Catches the silent failure nothing else covers: validators are internal, so omitting
    /// includeInternalTypes makes every one of them vanish and every request succeed.
    /// </summary>
    [Fact]
    public void EveryValidator_ResolvesFromTheContainer()
    {
        using ApiFixture fixture = new();

        Type[] validatorInterfaces = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType
                               && contract.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Distinct()
            .ToArray();

        validatorInterfaces.ShouldNotBeEmpty();

        using IServiceScope scope = fixture.Services.CreateScope();

        foreach (Type contract in validatorInterfaces)
        {
            scope.ServiceProvider.GetService(contract)
                .ShouldNotBeNull($"{contract.Name} is not registered — check includeInternalTypes.");
        }
    }

    private static EndpointFilterInvocationContext Context(Thing request) =>
        EndpointFilterInvocationContext.Create(new DefaultHttpContext(), request);

    private static EndpointFilterDelegate Next(Action onInvoked) => _ =>
    {
        onInvoked();

        return ValueTask.FromResult<object?>(Results.Ok());
    };

    private sealed record Thing(string Name);

    private sealed class ThingValidator : AbstractValidator<Thing>
    {
        public ThingValidator() => RuleFor(thing => thing.Name).NotEmpty();
    }
}
