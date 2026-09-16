using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Features.Topology.Shared;

/// <summary>
/// A topology element on the wire. The same shape appears on simulation issues and on generated-YAML
/// provenance, which is what lets the frontend match one to the other.
/// </summary>
/// <remarks>
/// <see cref="OwnerService"/> distinguishes a declaration from a use: a network with no owner is the
/// top-level declaration, and the same network with an owner is one service's attachment to it.
/// </remarks>
public sealed record ElementResponse(
    string Kind,
    string Name,
    string? OwnerService,
    string? Detail)
{
    public static ElementResponse From(ElementReference element) =>
        new(KindToken(element.Kind), element.Name, element.OwnerServiceName, element.Detail);

    private static string KindToken(ElementKind kind) => kind switch
    {
        ElementKind.Service => "service",
        ElementKind.Network => "network",
        ElementKind.Volume => "volume",
        ElementKind.PortMapping => "port_mapping",
        ElementKind.Dependency => "dependency",
        ElementKind.EnvironmentVariable => "environment_variable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped element kind.")
    };
}
