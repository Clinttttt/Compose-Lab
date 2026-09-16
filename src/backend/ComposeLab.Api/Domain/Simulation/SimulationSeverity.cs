namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// How much attention a finding deserves. Only <see cref="Error"/> makes a simulation unsuccessful.
/// </summary>
public enum SimulationSeverity
{
    Information,
    Warning,
    Error
}
