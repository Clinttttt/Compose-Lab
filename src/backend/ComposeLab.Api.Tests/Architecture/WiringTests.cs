using System.Reflection;
using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ComposeLab.Api.Tests.Architecture;

/// <summary>
/// The two tests that are not optional. Dispatch and routing are resolved at run time in this design,
/// so without these a missing handler registration is a 500 on first request and an undiscovered
/// endpoint is a silent 404. Both become build-time failures here.
/// </summary>
public sealed class WiringTests
{
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void EveryMessageHasExactlyOneRegisteredHandler()
    {
        using ApiFixture fixture = new();
        using IServiceScope scope = fixture.Services.CreateScope();

        (Type Message, Type HandlerContract)[] messages =
        [
            .. Api.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .SelectMany(type => type.GetInterfaces()
                    .Where(contract => contract.IsGenericType
                                       && contract.GetGenericTypeDefinition() == typeof(IQuery<>))
                    .Select(contract => (
                        Message: type,
                        HandlerContract: typeof(IQueryHandler<,>)
                            .MakeGenericType(type, contract.GenericTypeArguments[0]))))
        ];

        messages.ShouldNotBeEmpty();

        foreach ((Type message, Type contract) in messages)
        {
            scope.ServiceProvider.GetService(contract)
                .ShouldNotBeNull($"{message.FullName} has no registered handler.");
        }
    }

    [Fact]
    public void EveryEndpointIsDiscovered()
    {
        using ApiFixture fixture = new();

        int declared = Api.DefinedTypes
            .Count(type => type is { IsAbstract: false, IsInterface: false }
                           && type.IsAssignableTo(typeof(IEndpoint)));

        declared.ShouldBeGreaterThan(0);

        fixture.Services.GetServices<IEndpoint>().Count().ShouldBe(declared);
    }
}
