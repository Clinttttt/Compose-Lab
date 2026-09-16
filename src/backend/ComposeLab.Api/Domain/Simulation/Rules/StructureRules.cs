using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// Faults that make an architecture impossible to simulate at all, together with the explanation
/// each one produces. Every issue here is an error, and the pipeline stops rather than reporting
/// invented downstream behavior.
/// </summary>
internal static class StructureRules
{
    public static IReadOnlyList<SimulationIssue> Evaluate(NormalizedTopology topology)
    {
        List<SimulationIssue> issues = [];

        issues.AddRange(DuplicateServiceNames(topology));

        HashSet<string> declaredNetworks = [.. topology.Networks.Select(network => network.Name)];
        HashSet<string> declaredVolumes = [.. topology.Volumes.Select(volume => volume.Name)];
        HashSet<string> serviceNames = [.. topology.Services.Select(service => service.Name)];

        foreach (ContainerService service in topology.Services.OrderBy(service => service.Name, StringComparer.Ordinal))
        {
            if (!service.HasRunnableSource)
            {
                issues.Add(MissingRunnableSource(service));
            }

            foreach (NetworkAttachment attachment in service.Networks
                .Where(attachment => !declaredNetworks.Contains(attachment.NetworkName)))
            {
                issues.Add(UndeclaredNetwork(service, attachment));
            }

            foreach (VolumeMount mount in service.Volumes
                .Where(mount => !declaredVolumes.Contains(mount.VolumeName)))
            {
                issues.Add(UndeclaredVolume(service, mount));
            }

            foreach (ServiceDependency dependency in service.Dependencies)
            {
                if (dependency.ServiceName == service.Name)
                {
                    issues.Add(SelfDependency(service));
                }
                else if (!serviceNames.Contains(dependency.ServiceName))
                {
                    issues.Add(UnknownDependency(service, dependency));
                }
            }
        }

        issues.AddRange(DependencyCycles(topology));

        return issues;
    }

    private static IEnumerable<SimulationIssue> DuplicateServiceNames(NormalizedTopology topology) =>
        topology.Services
            .GroupBy(service => service.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SimulationIssue(
                SimulationIssueCode.DuplicateServiceName,
                SimulationSeverity.Error,
                [ElementReference.Service(group.Key)],
                WhatHappened: $"The service name '{group.Key}' is used {group.Count()} times.",
                Why: "A Compose service name is the identity of one workload. It is also the hostname other "
                    + "services use to reach it, so two services cannot share a name.",
                ArchitectureBehavior: $"Nothing can be resolved reliably: another service asking for "
                    + $"'{group.Key}' has no single answer, and ComposeLab cannot decide which definition to "
                    + "simulate.",
                SuggestedFix: $"Rename one of them, so each workload has its own name — for example "
                    + $"'{group.Key}' and '{group.Key}-2'."));

    private static SimulationIssue MissingRunnableSource(ContainerService service) =>
        new(
            SimulationIssueCode.ServiceWithoutImageOrBuild,
            SimulationSeverity.Error,
            [ElementReference.Service(service.Name)],
            WhatHappened: $"'{service.Name}' has neither an image nor a build context.",
            Why: "A service describes a container, and a container has to come from somewhere: either a "
                + "prebuilt image pulled from a registry, or an image built from a Dockerfile in a build "
                + "context.",
            ArchitectureBehavior: "There is nothing to start. Compose has no way to produce a container for "
                + "this service.",
            SuggestedFix: $"Give '{service.Name}' an image such as 'postgres:18', or a build context such as "
                + "'./Api' pointing at a folder with a Dockerfile.");

    private static SimulationIssue UndeclaredNetwork(ContainerService service, NetworkAttachment attachment) =>
        new(
            SimulationIssueCode.UndeclaredNetwork,
            SimulationSeverity.Error,
            [ElementReference.Service(service.Name), ElementReference.Network(attachment.NetworkName)],
            WhatHappened: $"'{service.Name}' is attached to the network '{attachment.NetworkName}', which is "
                + "not declared.",
            Why: "Every network a service joins must exist. Compose creates project networks from the "
                + "top-level 'networks' section, and the only network that exists without being declared is "
                + $"'{TopologyNormalizer.DefaultNetworkName}'.",
            ArchitectureBehavior: "Compose refuses to start the project rather than guessing what the network "
                + "should look like.",
            SuggestedFix: $"Declare '{attachment.NetworkName}' as a network, or attach '{service.Name}' to a "
                + "network that already exists.");

    private static SimulationIssue UndeclaredVolume(ContainerService service, VolumeMount mount) =>
        new(
            SimulationIssueCode.UndeclaredVolume,
            SimulationSeverity.Error,
            [ElementReference.Service(service.Name), ElementReference.Volume(mount.VolumeName)],
            WhatHappened: $"'{service.Name}' mounts the volume '{mount.VolumeName}' at "
                + $"'{mount.ContainerPath}', but that volume is not declared.",
            Why: "A named volume is a project-level resource. Mounting one means referring to a volume "
                + "declared in the top-level 'volumes' section.",
            ArchitectureBehavior: "Compose refuses to start the project, because it will not silently invent "
                + "storage that the architecture did not ask for.",
            SuggestedFix: $"Declare '{mount.VolumeName}' as a named volume.");

    private static SimulationIssue SelfDependency(ContainerService service) =>
        new(
            SimulationIssueCode.SelfDependency,
            SimulationSeverity.Error,
            [ElementReference.Dependency(service.Name, service.Name)],
            WhatHappened: $"'{service.Name}' depends on itself.",
            Why: "A dependency means 'start that one first'. A service cannot start before itself.",
            ArchitectureBehavior: "There is no valid start order, so the project cannot come up.",
            SuggestedFix: $"Remove '{service.Name}' from its own depends_on list.");

    private static SimulationIssue UnknownDependency(ContainerService service, ServiceDependency dependency) =>
        new(
            SimulationIssueCode.UnknownDependency,
            SimulationSeverity.Error,
            [ElementReference.Dependency(service.Name, dependency.ServiceName)],
            WhatHappened: $"'{service.Name}' depends on '{dependency.ServiceName}', which is not a service in "
                + "this architecture.",
            Why: "A dependency names another service in the same project. It is not a hostname, an image, or "
                + "a container name.",
            ArchitectureBehavior: "Compose cannot resolve the dependency and refuses to start the project.",
            SuggestedFix: $"Point the dependency at an existing service, or remove it if "
                + $"'{service.Name}' does not actually need to wait for anything.");

    /// <summary>
    /// Finds dependency cycles with an iterative depth-first search. Each cycle is reported once,
    /// keyed by its member set so that the same loop found from two different entry points does not
    /// produce two issues.
    /// </summary>
    private static IEnumerable<SimulationIssue> DependencyCycles(NormalizedTopology topology)
    {
        Dictionary<string, List<string>> edges = topology.Services
            .GroupBy(service => service.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Dependencies
                    .Select(dependency => dependency.ServiceName)
                    .Where(name => name != group.Key)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        HashSet<string> visited = new(StringComparer.Ordinal);
        HashSet<string> reported = new(StringComparer.Ordinal);
        List<SimulationIssue> issues = [];

        foreach (string start in edges.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            Walk(start, [], visited, edges, reported, issues);
        }

        return issues;
    }

    private static void Walk(
        string current,
        List<string> path,
        HashSet<string> visited,
        Dictionary<string, List<string>> edges,
        HashSet<string> reported,
        List<SimulationIssue> issues)
    {
        int existing = path.IndexOf(current);

        if (existing >= 0)
        {
            List<string> cycle = [.. path.Skip(existing), current];
            string key = string.Join('\u0000', cycle.Take(cycle.Count - 1).OrderBy(name => name, StringComparer.Ordinal));

            if (reported.Add(key))
            {
                issues.Add(DependencyCycle(cycle));
            }

            return;
        }

        if (!visited.Add(current) || !edges.TryGetValue(current, out List<string>? next))
        {
            return;
        }

        path.Add(current);

        foreach (string dependency in next)
        {
            Walk(dependency, path, visited, edges, reported, issues);
        }

        path.RemoveAt(path.Count - 1);
    }

    private static SimulationIssue DependencyCycle(IReadOnlyList<string> cycle) =>
        new(
            SimulationIssueCode.DependencyCycle,
            SimulationSeverity.Error,
            [.. cycle.Distinct(StringComparer.Ordinal).Select(ElementReference.Service)],
            WhatHappened: $"These services depend on each other in a loop: {string.Join(" \u2192 ", cycle)}.",
            Why: "Dependencies describe start order. A loop asks for each service to start before the one "
                + "that has to start before it.",
            ArchitectureBehavior: "No valid start order exists, so Compose refuses to start the project.",
            SuggestedFix: "Break the loop by removing one dependency. Services that genuinely need each "
                + "other at run time usually do not need to wait for each other at start time — they need "
                + "to retry a connection instead.");
}
