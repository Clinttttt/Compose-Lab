using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.Simulate;

/// <summary>
/// Maps the request to a topology, runs the simulator, and maps the outcome back.
/// </summary>
/// <remarks>
/// This handler always succeeds. Analyzing a broken architecture is a successful request that returns
/// <c>succeeded: false</c> — a 200 carrying an explanation, not a 4xx. Reporting a learner's
/// architectural mistake as a failed HTTP call would conflate "you asked me something invalid" with
/// "your architecture has a problem", and the second is the entire point of the endpoint.
/// </remarks>
internal sealed class Handler : IQueryHandler<Query, Response>
{
    public Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SimulationResult result = TopologySimulator.Simulate(TopologyDocumentMapper.ToTopology(query));

        return Task.FromResult(Result.Success(ToResponse(result)));
    }

    private static Response ToResponse(SimulationResult result) => new()
    {
        Succeeded = result.Succeeded,
        Completed = result.Completed,
        StartOrder = result.StartOrder,
        Events =
        [
            .. result.Events.Select(item => new SimulationEventResponse(
                item.Step,
                Name(item.Phase),
                item.Code.Value,
                ArchitectureIssueResponse.SeverityToken(item.Severity),
                [.. item.Elements.Select(ElementResponse.From)],
                item.Message))
        ],
        Issues = [.. result.Issues.Select(ArchitectureIssueResponse.From)],
        Reachability =
        [
            .. result.Reachability.Select(pair => new ReachabilityResponse(
                pair.ServiceA,
                pair.ServiceB,
                pair.CanCommunicate,
                pair.SharedNetworks))
        ],
        InferredConnections =
        [
            .. result.InferredConnections.Select(connection => new InferredConnectionResponse(
                connection.FromService,
                connection.ToService,
                connection.EnvironmentKey,
                connection.Host))
        ]
    };

    private static string Name(SimulationPhase phase) => phase switch
    {
        SimulationPhase.Normalization => "normalization",
        SimulationPhase.StructuralValidation => "structural_validation",
        SimulationPhase.ResourceCreation => "resource_creation",
        SimulationPhase.DependencyOrdering => "dependency_ordering",
        SimulationPhase.ServiceStart => "service_start",
        SimulationPhase.Reachability => "reachability",
        SimulationPhase.Completion => "completion",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unmapped simulation phase.")
    };
}
