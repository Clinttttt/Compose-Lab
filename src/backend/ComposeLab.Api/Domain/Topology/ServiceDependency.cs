namespace ComposeLab.Api.Domain.Topology;

/// <summary>A <c>depends_on</c> edge and the condition it waits for.</summary>
public sealed record ServiceDependency(string ServiceName, DependencyCondition Condition);
