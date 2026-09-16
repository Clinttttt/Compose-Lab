namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// An intent to communicate, inferred from an explicit host reference in environment configuration —
/// a recognized connection string or a URL.
/// </summary>
/// <remarks>
/// Labeled as inferred because it is a heuristic, not a declaration. A <c>depends_on</c> edge is
/// never a source of communication intent: it expresses lifecycle and readiness ordering, and
/// treating it as proof of network traffic would report working architectures as broken.
/// </remarks>
public sealed record InferredConnection(
    string FromService,
    string ToService,
    string EnvironmentKey,
    string Host);
