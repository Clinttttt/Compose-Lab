namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// How a published container port reaches the host. A service with no port mappings at all is a
/// third, distinct state — not published — and is represented by an empty
/// <see cref="ContainerService.Ports"/> collection rather than by a value here.
/// </summary>
public enum PortPublishMode
{
    /// <summary>The author chose the host port, so a collision with another service is possible.</summary>
    ExplicitHostPort,

    /// <summary>Docker assigns the host port at run time, so it cannot collide.</summary>
    DynamicHostPort
}
