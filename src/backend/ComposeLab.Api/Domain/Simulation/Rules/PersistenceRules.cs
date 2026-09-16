using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// Persistence and exposure lessons for services whose image is a recognized data store.
/// </summary>
/// <remarks>
/// The two checks here are related: a data store usually needs a volume, and usually does not need a
/// published port. Both are warnings rather than errors — the configuration is valid Compose, it is
/// the consequences the learner may not have intended.
/// </remarks>
internal static class PersistenceRules
{
    public static IReadOnlyList<SimulationIssue> Evaluate(NormalizedTopology topology)
    {
        List<SimulationIssue> issues = [];

        foreach (ContainerService service in topology.Services.OrderBy(service => service.Name, StringComparer.Ordinal))
        {
            StatefulImage? known = StatefulImageCatalog.Match(service.Image);

            if (known is null)
            {
                continue;
            }

            if (!IsDataPathPersisted(service, known.DataPath))
            {
                issues.Add(known.Expectation == PersistenceExpectation.UsuallyDurable
                    ? MissingPersistentVolume(service, known)
                    : OptionalPersistenceNotConfigured(service, known));
            }

            if (service.IsPublishedToHost)
            {
                issues.Add(StatefulServicePublished(service, known));
            }
        }

        return issues;
    }

    /// <summary>
    /// True when a named volume covers the image's data path — either mounted exactly there, or at a
    /// parent directory of it.
    /// </summary>
    private static bool IsDataPathPersisted(ContainerService service, string dataPath) =>
        service.Volumes.Any(mount =>
        {
            string mountPath = mount.ContainerPath.TrimEnd('/');

            return mountPath.Length > 0
                && (dataPath.Equals(mountPath, StringComparison.Ordinal)
                    || dataPath.StartsWith(mountPath + '/', StringComparison.Ordinal));
        });

    private static SimulationIssue MissingPersistentVolume(ContainerService service, StatefulImage known) =>
        new(
            SimulationIssueCode.MissingPersistentVolume,
            SimulationSeverity.Warning,
            [ElementReference.Service(service.Name)],
            WhatHappened: $"'{service.Name}' runs {known.DisplayName} with no volume covering "
                + $"'{known.DataPath}'.",
            Why: "A container's own filesystem is created with the container and destroyed with it. "
                + $"{known.DisplayName} writes its data to '{known.DataPath}', so without a named volume that "
                + "data lives only inside this particular container.",
            ArchitectureBehavior: "The database works normally while the container exists. The data disappears "
                + "the moment the container is removed and recreated — which happens whenever the service's "
                + "configuration changes, not only when someone deletes it deliberately. That is why this "
                + "usually shows up as data vanishing after an unrelated edit.",
            SuggestedFix: $"Declare a named volume and mount it at '{known.DataPath}'. A named volume exists "
                + "independently of any one container, so a new container reattaches to the same data.");

    private static SimulationIssue OptionalPersistenceNotConfigured(ContainerService service, StatefulImage known) =>
        new(
            SimulationIssueCode.OptionalPersistenceNotConfigured,
            SimulationSeverity.Information,
            [ElementReference.Service(service.Name)],
            WhatHappened: $"'{service.Name}' runs {known.DisplayName} with no volume covering "
                + $"'{known.DataPath}'.",
            Why: $"{known.DisplayName} can persist to '{known.DataPath}', but is often used as disposable "
                + "infrastructure where losing the contents on restart is acceptable or even intended.",
            ArchitectureBehavior: "Everything held by this service is lost when the container is recreated. "
                + "For a cache that is usually fine, because the data can be rebuilt from its source. For a "
                + "queue holding work that has not been processed yet, it usually is not.",
            SuggestedFix: $"If the contents matter, mount a named volume at '{known.DataPath}'. If this is a "
                + "cache and losing it is acceptable, no change is needed — this is a note, not a problem.");

    private static SimulationIssue StatefulServicePublished(ContainerService service, StatefulImage known) =>
        new(
            SimulationIssueCode.StatefulServicePublished,
            SimulationSeverity.Warning,
            [
                ElementReference.Service(service.Name),
                .. service.Ports.Select(port => ElementReference.Port(service.Name, port))
            ],
            WhatHappened: $"'{service.Name}' runs {known.DisplayName} and publishes a port to the host.",
            Why: "Services reach each other by service name on a shared network, using the container port. "
                + "The published host port is only needed for access from outside the project, so it is not "
                + "required for another service to connect.",
            ArchitectureBehavior: $"{known.DisplayName} is reachable from anything that can reach the host on "
                + "that port, which widens the exposure of the data store beyond the architecture that needs "
                + "it. The application would work identically with the port unpublished.",
            SuggestedFix: "Remove the published port unless you specifically want to connect a tool from your "
                + "own machine. If you do, that is a legitimate reason to keep it — just know it is for you, "
                + "not for the other services.");
}
