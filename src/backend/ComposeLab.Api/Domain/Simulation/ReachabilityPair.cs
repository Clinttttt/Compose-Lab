namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// Whether two services share a network, reported for every pair as information.
/// </summary>
/// <remarks>
/// Two services legitimately not sharing a network is a valid design — an isolated worker, a
/// deliberately private database tier — so the matrix is never turned into warnings. Only an
/// inferred connection that cannot be satisfied becomes an issue.
/// </remarks>
public sealed record ReachabilityPair(
    string ServiceA,
    string ServiceB,
    IReadOnlyList<string> SharedNetworks)
{
    public bool CanCommunicate => SharedNetworks.Count > 0;
}
