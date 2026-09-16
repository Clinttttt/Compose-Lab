namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// What a dependency waits for. Compose's short <c>depends_on</c> list form means
/// <see cref="ServiceStarted"/>.
/// </summary>
/// <remarks>
/// A dependency expresses lifecycle and readiness ordering. It does not express an intent to
/// communicate over the network, and ComposeLab must never treat it as evidence of one.
/// </remarks>
public enum DependencyCondition
{
    /// <summary>The container has started. This does not mean the application inside is ready.</summary>
    ServiceStarted,

    /// <summary>The dependency reports healthy before this service starts.</summary>
    ServiceHealthy
}
