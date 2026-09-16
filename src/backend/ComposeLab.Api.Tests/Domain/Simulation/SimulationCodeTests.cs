using System.Reflection;
using System.Text.RegularExpressions;
using ComposeLab.Api.Domain.Simulation;

namespace ComposeLab.Api.Tests.Domain.Simulation;

/// <summary>
/// Codes are typed internally and serialized as stable strings, so the compiler cannot police the
/// string form. These tests do it instead.
/// </summary>
public sealed partial class SimulationCodeTests
{
    [Fact]
    public void EveryIssueCodeIsUnique()
    {
        IReadOnlyList<string> codes = Values<SimulationIssueCode>();

        codes.ShouldNotBeEmpty();
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Count);
    }

    [Fact]
    public void EveryEventCodeIsUnique()
    {
        IReadOnlyList<string> codes = Values<SimulationEventCode>();

        codes.ShouldNotBeEmpty();
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Count);
    }

    [Fact]
    public void EveryIssueCodeIsADottedLowercaseToken()
    {
        foreach (string code in Values<SimulationIssueCode>())
        {
            CodeShape().IsMatch(code).ShouldBeTrue($"'{code}' is not a stable dotted lowercase code.");
        }
    }

    [Fact]
    public void EveryEventCodeIsADottedLowercaseToken()
    {
        foreach (string code in Values<SimulationEventCode>())
        {
            CodeShape().IsMatch(code).ShouldBeTrue($"'{code}' is not a stable dotted lowercase code.");
        }
    }

    [Fact]
    public void CodesCompareByValue()
    {
        SimulationIssueCode.NetworkUnreachable.ShouldBe(SimulationIssueCode.NetworkUnreachable);
        SimulationIssueCode.NetworkUnreachable.ShouldNotBe(SimulationIssueCode.HostPortCollision);
        SimulationIssueCode.NetworkUnreachable.Value.ShouldBe("network.unreachable");
        SimulationIssueCode.NetworkUnreachable.ToString().ShouldBe("network.unreachable");
    }

    private static IReadOnlyList<string> Values<TCode>() =>
    [
        .. typeof(TCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(TCode))
            .Select(field => field.GetValue(null)!.ToString()!)
    ];

    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}
