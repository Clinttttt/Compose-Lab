namespace ComposeLab.Api.Domain.Topology.Document;

/// <summary>
/// The version of the topology document contract.
/// </summary>
/// <remarks>
/// Recorded alongside every saved project from the first release. Without it, a later change to the
/// contract would make old rows ambiguous: there would be no way to tell a document that omits a field
/// from one written before the field existed. Cheap now, impossible to add retroactively.
/// </remarks>
public static class TopologySchema
{
    public const int CurrentVersion = 1;
}
