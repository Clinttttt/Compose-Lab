using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// Host port claims. A collision is keyed by host port <em>and</em> protocol, because TCP 8080 and
/// UDP 8080 are different claims and do not conflict.
/// </summary>
internal static class PortRules
{
    public static IReadOnlyList<SimulationIssue> Evaluate(NormalizedTopology topology) =>
    [
        .. topology.Services
            .SelectMany(service => service.Ports
                .Where(port => port.HostPort is not null)
                .Select(port => (Service: service, Port: port)))
            .GroupBy(claim => (HostPort: claim.Port.HostPort!.Value, claim.Port.Protocol))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.HostPort)
            .ThenBy(group => group.Key.Protocol)
            .Select(group => HostPortCollision(group.Key.HostPort, group.Key.Protocol, [.. group]))
    ];

    private static SimulationIssue HostPortCollision(
        int hostPort,
        PortProtocol protocol,
        IReadOnlyList<(ContainerService Service, PortMapping Port)> claims)
    {
        string protocolToken = claims[0].Port.ProtocolToken;
        string port = hostPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        string claimants = string.Join(
            ", ",
            claims
                .OrderBy(claim => claim.Service.Name, StringComparer.Ordinal)
                .Select(claim => $"'{claim.Service.Name}' ({claim.Port.ToComposeSyntax()})"));

        return new SimulationIssue(
            SimulationIssueCode.HostPortCollision,
            SimulationSeverity.Error,
            [.. claims.Select(claim => ElementReference.Port(claim.Service.Name, claim.Port))],
            WhatHappened: $"Host port {port}/{protocolToken} is claimed more than once: {claimants}.",
            Why: "A published port is a reservation on the host's network interface. Only one process on the "
                + "host can hold a given port and protocol at a time, so two services cannot publish the "
                + "same one.",
            ArchitectureBehavior: $"The first container to bind {port}/{protocolToken} gets it. The next one "
                + "fails to start with an address-already-in-use error, and which one loses depends on start "
                + "order rather than on anything in the configuration.",
            SuggestedFix: $"Give each service its own host port — keep {port} for one of them and move the "
                + "others. The container port does not need to change, only the host side of the mapping. If "
                + "a service only needs to be reachable by other services, it does not need a published port "
                + "at all.");
    }
}
