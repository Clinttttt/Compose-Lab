namespace ComposeLab.Api.Domain.Topology;

/// <summary>The kind of topology element a reference points at.</summary>
public enum ElementKind
{
    Service,
    Network,
    Volume,
    PortMapping,
    Dependency,
    EnvironmentVariable
}
