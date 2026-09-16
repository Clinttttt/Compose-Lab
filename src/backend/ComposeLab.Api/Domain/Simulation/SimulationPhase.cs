namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// The stage of the simulation an event belongs to. Phases give the console its structure without
/// inventing timestamps: ComposeLab reports ordered steps, never a fabricated clock.
/// </summary>
public enum SimulationPhase
{
    Normalization,
    StructuralValidation,
    ResourceCreation,
    DependencyOrdering,
    ServiceStart,
    Reachability,

    /// <summary>The run's outcome. Not an analysis stage — it exists so the console can label a result.</summary>
    Completion
}
