using System.Reflection;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Extensions;
using FluentValidation;

namespace ComposeLab.Api.DependencyInjection;

public static class ApplicationServices
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(Program).Assembly;

        // includeInternalTypes is required: validators are internal. Without it the validation filter
        // finds no validator, calls next, and every request succeeds.
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddEndpoints(assembly);

        // publicOnly: false is required for the same reason: handlers are internal.
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
