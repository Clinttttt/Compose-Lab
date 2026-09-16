using ComposeLab.Api.Domain.Simulation.Rules;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// Answers whether an architecture is sound, without producing the simulation timeline.
/// </summary>
/// <remarks>
/// Validation and simulation are separate on purpose. Validation is cheap enough to run after every edit
/// in the inspector and returns a short list; simulation is the deliberate "run it" action that produces
/// the ordered story of what would happen. They read from the same rule catalog, so an architecture can
/// never be sound according to one and broken according to the other.
/// </remarks>
public static class TopologyValidator
{
    public static ValidationReport Validate(ApplicationTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        IReadOnlyList<SimulationIssue> structural = RuleCatalog.Structural(normalized);

        if (RuleCatalog.HasError(structural))
        {
            return new ValidationReport
            {
                IsValid = false,
                IsComplete = false,
                Issues = structural
            };
        }

        List<SimulationIssue> issues =
        [
            .. structural,
            .. RuleCatalog.Advisory(normalized, ConnectionIntentInference.Infer(normalized))
        ];

        return new ValidationReport
        {
            IsValid = !RuleCatalog.HasError(issues),
            IsComplete = true,
            Issues = issues
        };
    }
}
