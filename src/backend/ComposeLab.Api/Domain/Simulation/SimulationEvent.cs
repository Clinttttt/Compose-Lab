using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// One step on the simulation timeline. Flat by design: a polymorphic hierarchy would buy nothing
/// the frontend can use and would cost polymorphic JSON on the wire.
/// </summary>
public sealed record SimulationEvent(
    int Step,
    SimulationPhase Phase,
    SimulationEventCode Code,
    SimulationSeverity Severity,
    IReadOnlyList<ElementReference> Elements,
    string Message);
