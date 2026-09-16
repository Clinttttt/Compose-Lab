using System.Reflection;
using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using NetArchTest.Rules;

namespace ComposeLab.Api.Tests.Architecture;

/// <summary>
/// The boundaries are these tests; the folder layout is only their shadow. Rules that turn on open
/// generic interfaces use reflection rather than NetArchTest, which does not reliably match a closed
/// implementation of an open generic contract.
/// </summary>
public sealed class StructureTests
{
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string Features = "ComposeLab.Api.Features";
    private const string Domain = "ComposeLab.Api.Domain";
    private const string Abstractions = "ComposeLab.Api.Abstractions";
    private const string Behaviors = "ComposeLab.Api.Behaviors";
    private const string Middleware = "ComposeLab.Api.Middleware";
    private const string DependencyInjection = "ComposeLab.Api.DependencyInjection";

    [Fact]
    public void DomainDependsOnNothingElseInTheApplication() =>
        Types.InAssembly(Api)
            .That().ResideInNamespace(Domain)
            .ShouldNot().HaveDependencyOnAny(
                Features,
                Behaviors,
                Middleware,
                DependencyInjection,
                "ComposeLab.Api.Extensions",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "FluentValidation")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void AbstractionsDoNotKnowAboutFeaturesOrPlumbing() =>
        Types.InAssembly(Api)
            .That().ResideInNamespace(Abstractions)
            .ShouldNot().HaveDependencyOnAny(
                Features,
                Behaviors,
                Middleware,
                DependencyInjection,
                "Microsoft.AspNetCore.Mvc",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void DomainDoesNotDependOnTheSlices() =>
        Types.InAssembly(Api)
            .That().ResideInNamespace($"{Domain}.Simulation")
            .ShouldNot().HaveDependencyOn(Features)
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void NoSliceReferencesAnotherFeature()
    {
        string[] features = FeatureNamespaces(depth: 4);

        features.ShouldNotBeEmpty();

        foreach (string source in features)
        {
            string[] others = [.. features.Where(candidate => candidate != source)];

            if (others.Length == 0)
            {
                continue;
            }

            Types.InAssembly(Api)
                .That().ResideInNamespace(source)
                .ShouldNot().HaveDependencyOnAny(others)
                .GetResult().IsSuccessful
                .ShouldBeTrue($"{source} references another feature.");
        }
    }

    /// <summary>
    /// <c>Shared/</c> is excluded on purpose: it is the one bucket the design permits siblings to reach
    /// into, which is why the contracts every engine slice needs live there rather than being duplicated.
    /// </summary>
    [Fact]
    public void NoSliceReferencesAnotherUseCase()
    {
        string[] useCases = FeatureNamespaces(depth: 5);

        useCases.ShouldNotBeEmpty();

        foreach (string source in useCases.Where(space => !space.EndsWith(".Shared", StringComparison.Ordinal)))
        {
            string[] others =
            [
                .. useCases.Where(candidate =>
                    candidate != source && !candidate.EndsWith(".Shared", StringComparison.Ordinal))
            ];

            if (others.Length == 0)
            {
                continue;
            }

            Types.InAssembly(Api)
                .That().ResideInNamespace(source)
                .ShouldNot().HaveDependencyOnAny(others)
                .GetResult().IsSuccessful
                .ShouldBeTrue($"{source} references another use case.");
        }
    }

    /// <summary>The shared bucket is a leaf: it may not reach back into the slices that use it.</summary>
    [Fact]
    public void TheSharedBucketDoesNotReferenceAnySlice()
    {
        string[] slices =
        [
            .. FeatureNamespaces(depth: 5).Where(space => !space.EndsWith(".Shared", StringComparison.Ordinal))
        ];

        slices.ShouldNotBeEmpty();

        Types.InAssembly(Api)
            .That().ResideInNamespace($"{Features}.Topology.Shared")
            .ShouldNot().HaveDependencyOnAny(slices)
            .GetResult().IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void NoSliceReferencesTheRegistrationRoot() =>
        Types.InAssembly(Api)
            .That().ResideInNamespace(Features)
            .ShouldNot().HaveDependencyOn(DependencyInjection)
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void HandlersAreNotPublic()
    {
        Type[] handlers = Implementations(typeof(IQueryHandler<,>));

        handlers.ShouldNotBeEmpty();

        foreach (Type handler in handlers)
        {
            handler.IsPublic.ShouldBeFalse($"{handler.FullName} is public.");
        }
    }

    [Fact]
    public void EndpointsAreNotPublic()
    {
        Type[] endpoints =
        [
            .. Api.DefinedTypes.Where(type => type is { IsAbstract: false, IsInterface: false }
                                              && type.IsAssignableTo(typeof(IEndpoint)))
        ];

        endpoints.ShouldNotBeEmpty();

        foreach (Type endpoint in endpoints)
        {
            endpoint.IsPublic.ShouldBeFalse($"{endpoint.FullName} is public.");
        }
    }

    [Fact]
    public void MessagesDoNotExposeTransportTypes()
    {
        Type[] messages = Implementations(typeof(IQuery<>));

        messages.ShouldNotBeEmpty();

        foreach (Type message in messages)
        {
            foreach (PropertyInfo property in message.GetProperties())
            {
                foreach (Type type in Involved(property.PropertyType))
                {
                    string? space = type.Namespace;

                    if (space is null)
                    {
                        continue;
                    }

                    space.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                        .ShouldBeFalse($"{message.Name}.{property.Name} exposes {type.Name}.");

                    space.StartsWith("System.Security.Claims", StringComparison.Ordinal)
                        .ShouldBeFalse($"{message.Name}.{property.Name} exposes {type.Name}.");
                }
            }
        }
    }

    private static Type[] Implementations(Type openGeneric) =>
    [
        .. Api.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && type.GetInterfaces().Any(contract =>
                               contract.IsGenericType
                               && contract.GetGenericTypeDefinition() == openGeneric))
    ];

    private static IEnumerable<Type> Involved(Type type)
    {
        yield return type;

        foreach (Type argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            yield return argument;
        }
    }

    private static string[] FeatureNamespaces(int depth) =>
    [
        .. Api.GetTypes()
            .Select(type => type.Namespace)
            .Where(space => space is not null && space.StartsWith($"{Features}.", StringComparison.Ordinal))
            .Select(space => string.Join('.', space!.Split('.').Take(depth)))
            .Distinct(StringComparer.Ordinal)
    ];
}
