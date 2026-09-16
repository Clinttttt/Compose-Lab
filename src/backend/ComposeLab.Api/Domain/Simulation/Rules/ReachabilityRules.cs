using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// Whether services can reach each other. Reachability is a shared-network set intersection, which is
/// only correct because normalization has already materialized the implicit <c>default</c> network.
/// </summary>
internal static class ReachabilityRules
{
    public static IReadOnlyList<ReachabilityPair> BuildMatrix(NormalizedTopology topology)
    {
        List<ContainerService> ordered = [.. topology.Services.OrderBy(service => service.Name, StringComparer.Ordinal)];
        List<ReachabilityPair> pairs = [];

        for (int first = 0; first < ordered.Count; first++)
        {
            for (int second = first + 1; second < ordered.Count; second++)
            {
                pairs.Add(new ReachabilityPair(
                    ordered[first].Name,
                    ordered[second].Name,
                    SharedNetworks(ordered[first], ordered[second])));
            }
        }

        return pairs;
    }

    public static IReadOnlyList<string> SharedNetworks(ContainerService first, ContainerService second) =>
    [
        .. first.Networks
            .Select(attachment => attachment.NetworkName)
            .Intersect(second.Networks.Select(attachment => attachment.NetworkName), StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
    ];

    public static IReadOnlyList<SimulationIssue> Evaluate(
        NormalizedTopology topology,
        IReadOnlyList<InferredConnection> connections)
    {
        List<SimulationIssue> issues = [];

        IEnumerable<IGrouping<(string From, string To), InferredConnection>> intents = connections
            .GroupBy(connection => (From: connection.FromService, To: connection.ToService))
            .OrderBy(group => group.Key.From, StringComparer.Ordinal)
            .ThenBy(group => group.Key.To, StringComparer.Ordinal);

        foreach (IGrouping<(string From, string To), InferredConnection> intent in intents)
        {
            ContainerService? from = topology.FindService(intent.Key.From);
            ContainerService? to = topology.FindService(intent.Key.To);

            if (from is null || to is null || SharedNetworks(from, to).Count > 0)
            {
                continue;
            }

            issues.Add(Unreachable(from, to, [.. intent]));
        }

        return issues;
    }

    private static SimulationIssue Unreachable(
        ContainerService from,
        ContainerService to,
        IReadOnlyList<InferredConnection> intents)
    {
        string fromNetworks = Describe(from);
        string toNetworks = Describe(to);

        string keys = string.Join(
            ", ",
            intents
                .Select(intent => intent.EnvironmentKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal));

        List<ElementReference> elements =
        [
            ElementReference.Service(from.Name),
            ElementReference.Service(to.Name),
            .. intents
                .Select(intent => ElementReference.EnvironmentVariable(intent.FromService, intent.EnvironmentKey))
                .Distinct()
        ];

        return new SimulationIssue(
            SimulationIssueCode.NetworkUnreachable,
            SimulationSeverity.Error,
            elements,
            WhatHappened: $"'{from.Name}' cannot reach '{to.Name}'.",
            Why: $"'{from.Name}' is attached to {fromNetworks}. '{to.Name}' is attached to {toNetworks}. They "
                + "share no network, and Compose only carries traffic between services that are on a network "
                + "together.",
            ArchitectureBehavior: $"'{from.Name}' is configured to reach '{to.Name}' through {keys}, but the "
                + $"name '{to.Name}' cannot be resolved from inside '{from.Name}': Compose's internal DNS only "
                + "answers for services on the same network. The connection fails at run time with a host "
                + "resolution error, not with a configuration error, which is why this kind of mistake usually "
                + "surfaces as a confusing application crash.",
            SuggestedFix: $"Put both services on one shared network. Attaching '{from.Name}' and '{to.Name}' "
                + "to the same network is enough — no published ports are involved, because service-to-service "
                + "traffic never goes through the host.");
    }

    private static string Describe(ContainerService service)
    {
        if (service.Networks.Count == 0)
        {
            return "no network";
        }

        IEnumerable<string> names = service.Networks
            .OrderBy(attachment => attachment.NetworkName, StringComparer.Ordinal)
            .Select(attachment => attachment.Origin == DeclarationOrigin.Implicit
                ? $"'{attachment.NetworkName}' (implied by Compose)"
                : $"'{attachment.NetworkName}'");

        return string.Join(" and ", names);
    }
}
