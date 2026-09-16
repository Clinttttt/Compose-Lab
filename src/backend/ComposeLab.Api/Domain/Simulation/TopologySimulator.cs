using System.Globalization;
using ComposeLab.Api.Domain.Simulation.Rules;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// Runs the deterministic pipeline: normalize, validate structure, create resources, resolve start
/// order, represent service start, then evaluate reachability.
/// </summary>
/// <remarks>
/// The same architecture always produces the same timeline. Steps are numbered rather than
/// timestamped: a fabricated clock would invite exactly the confusion between simulation and real
/// execution that ComposeLab has to avoid.
/// </remarks>
public static class TopologySimulator
{
    public static SimulationResult Simulate(ApplicationTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);
        EventLog log = new();

        log.Add(
            SimulationPhase.Normalization,
            SimulationEventCode.TopologyNormalized,
            NormalizationMessage(normalized),
            [.. normalized.Networks
                .Where(network => network.Origin == DeclarationOrigin.Implicit)
                .Select(network => ElementReference.Network(network.Name))]);

        List<SimulationIssue> issues =
        [
            .. StructureRules.Evaluate(normalized),
            .. PortRules.Evaluate(normalized)
        ];

        if (issues.Any(issue => issue.Severity == SimulationSeverity.Error))
        {
            log.Add(
                SimulationPhase.StructuralValidation,
                SimulationEventCode.SimulationAborted,
                $"Stopped before starting anything: {Count(issues.Count(issue => issue.Severity == SimulationSeverity.Error), "fault", "faults")} "
                    + "would prevent this architecture from running at all. Anything reported after this point "
                    + "would be guesswork.",
                [],
                SimulationSeverity.Error);

            return new SimulationResult
            {
                Succeeded = false,
                Completed = false,
                Events = log.Events,
                Issues = issues
            };
        }

        CreateResources(normalized, log);

        IReadOnlyList<string> startOrder = ServiceStartOrder.Resolve(normalized);

        log.Add(
            SimulationPhase.DependencyOrdering,
            SimulationEventCode.StartOrderResolved,
            $"Start order: {string.Join(" \u2192 ", startOrder)}. Services with no dependency between them are "
                + "listed alphabetically for readability; Docker would start those at the same time.",
            [.. startOrder.Select(ElementReference.Service)]);

        StartServices(normalized, startOrder, log);

        issues.AddRange(ReadinessRules.Evaluate(normalized));
        issues.AddRange(PersistenceRules.Evaluate(normalized));

        IReadOnlyList<InferredConnection> inferred = ConnectionIntentInference.Infer(normalized);
        IReadOnlyList<ReachabilityPair> reachability = ReachabilityRules.BuildMatrix(normalized);

        issues.AddRange(ReachabilityRules.Evaluate(normalized, inferred));

        log.Add(
            SimulationPhase.Reachability,
            SimulationEventCode.ReachabilityEvaluated,
            ReachabilityMessage(reachability, inferred),
            []);

        int errors = issues.Count(issue => issue.Severity == SimulationSeverity.Error);
        int warnings = issues.Count(issue => issue.Severity == SimulationSeverity.Warning);

        log.Add(
            SimulationPhase.Completion,
            SimulationEventCode.SimulationCompleted,
            CompletionMessage(errors, warnings),
            [],
            errors > 0 ? SimulationSeverity.Error : SimulationSeverity.Information);

        return new SimulationResult
        {
            Succeeded = errors == 0,
            Completed = true,
            StartOrder = startOrder,
            Events = log.Events,
            Issues = issues,
            Reachability = reachability,
            InferredConnections = inferred
        };
    }

    private static void CreateResources(NormalizedTopology topology, EventLog log)
    {
        foreach (ContainerNetwork network in topology.Networks.OrderBy(network => network.Name, StringComparer.Ordinal))
        {
            log.Add(
                SimulationPhase.ResourceCreation,
                SimulationEventCode.NetworkCreated,
                network.Origin == DeclarationOrigin.Implicit
                    ? $"Created network '{network.Name}'. You did not declare it — Compose creates one network "
                        + "per project and attaches every service that names none of its own."
                    : $"Created network '{network.Name}'.",
                [ElementReference.Network(network.Name)]);
        }

        foreach (ContainerVolume volume in topology.Volumes.OrderBy(volume => volume.Name, StringComparer.Ordinal))
        {
            log.Add(
                SimulationPhase.ResourceCreation,
                SimulationEventCode.VolumeCreated,
                $"Created named volume '{volume.Name}'. It exists independently of any container, so removing "
                    + "a container does not remove it.",
                [ElementReference.Volume(volume.Name)]);
        }
    }

    private static void StartServices(NormalizedTopology topology, IReadOnlyList<string> startOrder, EventLog log)
    {
        foreach (string name in startOrder)
        {
            ContainerService? service = topology.FindService(name);

            if (service is null)
            {
                continue;
            }

            foreach (ServiceDependency dependency in service.Dependencies
                .OrderBy(dependency => dependency.ServiceName, StringComparer.Ordinal))
            {
                log.Add(
                    SimulationPhase.ServiceStart,
                    SimulationEventCode.DependencySatisfied,
                    dependency.Condition == DependencyCondition.ServiceHealthy
                        ? $"'{service.Name}' waited for '{dependency.ServiceName}' to report healthy. ComposeLab "
                            + "reads health as declared configuration and does not run the check itself."
                        : $"'{service.Name}' waited for '{dependency.ServiceName}' to start. Started is not the "
                            + "same as ready.",
                    [ElementReference.Dependency(service.Name, dependency.ServiceName)]);
            }

            foreach (NetworkAttachment attachment in service.Networks
                .OrderBy(attachment => attachment.NetworkName, StringComparer.Ordinal))
            {
                log.Add(
                    SimulationPhase.ServiceStart,
                    SimulationEventCode.NetworkAttached,
                    attachment.Origin == DeclarationOrigin.Implicit
                        ? $"'{service.Name}' attached to '{attachment.NetworkName}', which Compose supplied "
                            + "because the service declares no networks of its own."
                        : $"'{service.Name}' attached to '{attachment.NetworkName}'.",
                    [
                        ElementReference.Service(service.Name),
                        ElementReference.NetworkAttachment(service.Name, attachment.NetworkName),
                        ElementReference.Network(attachment.NetworkName)
                    ]);
            }

            foreach (VolumeMount mount in service.Volumes
                .OrderBy(mount => mount.ContainerPath, StringComparer.Ordinal))
            {
                log.Add(
                    SimulationPhase.ServiceStart,
                    SimulationEventCode.VolumeMounted,
                    $"'{service.Name}' mounted '{mount.VolumeName}' at '{mount.ContainerPath}'.",
                    [
                        ElementReference.Service(service.Name),
                        ElementReference.VolumeMount(service.Name, mount),
                        ElementReference.Volume(mount.VolumeName)
                    ]);
            }

            if (service.IsPublishedToHost)
            {
                foreach (PortMapping port in service.Ports
                    .OrderBy(port => port.ContainerPort)
                    .ThenBy(port => port.HostPort ?? 0))
                {
                    log.Add(
                        SimulationPhase.ServiceStart,
                        SimulationEventCode.PortPublished,
                        port.HostPort is null
                            ? $"'{service.Name}' published container port "
                                + $"{Number(port.ContainerPort)}/{port.ProtocolToken} on a host port Docker "
                                + "assigns at run time."
                            : $"Host port {Number(port.HostPort.Value)}/{port.ProtocolToken} forwards to "
                                + $"'{service.Name}' on container port {Number(port.ContainerPort)}.",
                        [ElementReference.Port(service.Name, port)]);
                }
            }
            else
            {
                log.Add(
                    SimulationPhase.ServiceStart,
                    SimulationEventCode.ServiceNotPublished,
                    $"'{service.Name}' is not published to the host. Other services still reach it by name on a "
                        + "shared network, using its container port.",
                    [ElementReference.Service(service.Name)]);
            }

            log.Add(
                SimulationPhase.ServiceStart,
                SimulationEventCode.ServiceStarted,
                $"'{service.Name}' started.",
                [ElementReference.Service(service.Name)]);
        }
    }

    private static string NormalizationMessage(NormalizedTopology topology)
    {
        if (!topology.DefaultNetworkWasMaterialized)
        {
            return "Read the architecture. Every service names the networks it belongs to.";
        }

        int implicitAttachments = topology.Services.Count(service =>
            service.Networks.Any(attachment =>
                attachment.Origin == DeclarationOrigin.Implicit
                && attachment.NetworkName == TopologyNormalizer.DefaultNetworkName));

        return $"Read the architecture. {Count(implicitAttachments, "service declares", "services declare")} no "
            + $"network, so Compose's '{TopologyNormalizer.DefaultNetworkName}' network applies to "
            + "them — that is why services can often reach each other without any network being written down.";
    }

    private static string ReachabilityMessage(
        IReadOnlyList<ReachabilityPair> reachability,
        IReadOnlyList<InferredConnection> inferred)
    {
        string pairs = reachability.Count == 0
            ? "There is only one service, so there is nothing to connect."
            : $"Checked {Count(reachability.Count, "service pair", "service pairs")}, "
                + $"{Number(reachability.Count(pair => pair.CanCommunicate))} of which share a network.";

        string intents = inferred.Count == 0
            ? " No intended connection could be read from the environment configuration, so reachability is "
                + "reported for information only."
            : $" Found {Count(inferred.Count, "intended connection", "intended connections")} in environment "
                + "configuration.";

        return pairs + intents;
    }

    private static string CompletionMessage(int errors, int warnings)
    {
        if (errors > 0)
        {
            return $"Finished with {Count(errors, "problem", "problems")} that would stop this architecture from "
                + "working as intended.";
        }

        return warnings > 0
            ? $"Finished. The architecture works, with {Count(warnings, "point", "points")} worth reviewing."
            : "Finished. The architecture works.";
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Count(int value, string singular, string plural) =>
        $"{Number(value)} {(value == 1 ? singular : plural)}";

    /// <summary>Collects timeline events and assigns their step numbers.</summary>
    private sealed class EventLog
    {
        private readonly List<SimulationEvent> _events = [];

        public IReadOnlyList<SimulationEvent> Events => _events;

        public void Add(
            SimulationPhase phase,
            SimulationEventCode code,
            string message,
            IReadOnlyList<ElementReference> elements,
            SimulationSeverity severity = SimulationSeverity.Information) =>
            _events.Add(new SimulationEvent(_events.Count + 1, phase, code, severity, elements, message));
    }
}
