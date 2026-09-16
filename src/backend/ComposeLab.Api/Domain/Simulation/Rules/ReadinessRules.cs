using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// What a dependency actually guarantees.
/// </summary>
/// <remarks>
/// The lesson this family exists to teach is that startup ordering is not application readiness. The
/// limit it must never overstate is that ComposeLab models health as a declared property and does not
/// execute the check, so a missing healthcheck is an unverifiable condition rather than invalid
/// Compose — the image may well declare <c>HEALTHCHECK</c> in its Dockerfile.
/// </remarks>
internal static class ReadinessRules
{
    public static IReadOnlyList<SimulationIssue> Evaluate(NormalizedTopology topology)
    {
        List<SimulationIssue> issues = [];

        foreach (ContainerService service in topology.Services.OrderBy(service => service.Name, StringComparer.Ordinal))
        {
            foreach (ServiceDependency dependency in service.Dependencies
                .OrderBy(dependency => dependency.ServiceName, StringComparer.Ordinal))
            {
                ContainerService? target = topology.FindService(dependency.ServiceName);

                if (target is null)
                {
                    continue;
                }

                if (dependency.Condition == DependencyCondition.ServiceStarted)
                {
                    issues.Add(OrderingIsNotReadiness(service, target));

                    if (target.HasEnabledHealthCheck)
                    {
                        issues.Add(HealthCheckAvailable(service, target));
                    }

                    continue;
                }

                if (target.HealthCheck is null)
                {
                    issues.Add(HealthConditionUnverifiable(service, target));
                }
                else if (!target.HealthCheck.IsEnabled)
                {
                    issues.Add(HealthConditionDisabled(service, target));
                }
            }
        }

        return issues;
    }

    private static SimulationIssue OrderingIsNotReadiness(ContainerService service, ContainerService target) =>
        new(
            SimulationIssueCode.OrderingIsNotReadiness,
            SimulationSeverity.Information,
            [ElementReference.Dependency(service.Name, target.Name)],
            WhatHappened: $"'{service.Name}' waits for '{target.Name}' to start.",
            Why: "The short depends_on form waits for the container to start, which is not the same as the "
                + "program inside it being ready to accept work. A database container is 'started' well before "
                + "it finishes initializing and begins listening.",
            ArchitectureBehavior: $"'{service.Name}' may start while '{target.Name}' is still coming up, so its "
                + "first connection attempt can fail even though the start order is exactly what was asked "
                + "for.",
            SuggestedFix: "Treat this as expected and make the application retry its connection on startup. "
                + "Where a stricter guarantee is needed, a healthcheck plus condition: service_healthy makes "
                + "Compose wait for readiness rather than for the container.");

    private static SimulationIssue HealthCheckAvailable(ContainerService service, ContainerService target) =>
        new(
            SimulationIssueCode.HealthCheckAvailable,
            SimulationSeverity.Warning,
            [ElementReference.Dependency(service.Name, target.Name)],
            WhatHappened: $"'{service.Name}' waits only for '{target.Name}' to start, even though "
                + $"'{target.Name}' declares an enabled healthcheck.",
            Why: "The stronger guarantee is already available. A declared healthcheck is what lets Compose "
                + "wait for readiness instead of for the container process.",
            ArchitectureBehavior: $"As written, '{service.Name}' can start before '{target.Name}' is ready, and "
                + "the healthcheck has no effect on start order.",
            SuggestedFix: $"Change the dependency on '{target.Name}' to condition: service_healthy so the "
                + "declared healthcheck is actually used.");

    private static SimulationIssue HealthConditionUnverifiable(ContainerService service, ContainerService target) =>
        new(
            SimulationIssueCode.HealthConditionUnverifiable,
            SimulationSeverity.Warning,
            [ElementReference.Dependency(service.Name, target.Name)],
            WhatHappened: $"'{service.Name}' waits for '{target.Name}' to become healthy, but no healthcheck "
                + $"for '{target.Name}' is present in this architecture.",
            Why: "This is not necessarily wrong. A health condition needs a healthcheck to exist, and an image "
                + "can declare one in its Dockerfile that the Compose file never mentions. ComposeLab only "
                + "sees the Compose configuration, so it cannot confirm one either way.",
            ArchitectureBehavior: $"If the '{target.Name}' image declares its own healthcheck, this works as "
                + "intended. If it does not, Compose has no health state to wait for and the dependency cannot "
                + "be satisfied.",
            SuggestedFix: $"Declare the healthcheck on '{target.Name}' in the Compose file. Doing so makes the "
                + "requirement explicit to anyone reading the architecture, and lets ComposeLab verify it.");

    private static SimulationIssue HealthConditionDisabled(ContainerService service, ContainerService target) =>
        new(
            SimulationIssueCode.HealthConditionDisabled,
            SimulationSeverity.Warning,
            [ElementReference.Dependency(service.Name, target.Name)],
            WhatHappened: $"'{service.Name}' waits for '{target.Name}' to become healthy, but the healthcheck "
                + $"on '{target.Name}' is explicitly disabled.",
            Why: "Disabling a healthcheck turns off health reporting for the service, including any check the "
                + "image declares. The condition then has nothing to observe.",
            ArchitectureBehavior: $"'{target.Name}' never reports a health state, so the dependency cannot be "
                + "satisfied as written.",
            SuggestedFix: $"Either enable the healthcheck on '{target.Name}', or change the dependency back to "
                + "waiting for start and let the application retry its connection.");
}
