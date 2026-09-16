using ComposeLab.Api.Domain.Simulation;

namespace ComposeLab.Api.Tests.TestSupport;

internal static class SimulationAssertions
{
    public static IReadOnlyList<SimulationIssue> Coded(this SimulationResult result, SimulationIssueCode code) =>
        [.. result.Issues.Where(issue => issue.Code == code)];

    public static bool Has(this SimulationResult result, SimulationIssueCode code) =>
        result.Issues.Any(issue => issue.Code == code);

    public static SimulationIssue Only(this SimulationResult result, SimulationIssueCode code) =>
        result.Coded(code).ShouldHaveSingleItem();

    public static IReadOnlyList<SimulationIssue> Errors(this SimulationResult result) =>
        [.. result.Issues.Where(issue => issue.Severity == SimulationSeverity.Error)];

    public static bool Logged(this SimulationResult result, SimulationEventCode code) =>
        result.Events.Any(item => item.Code == code);
}
