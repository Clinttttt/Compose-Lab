using System.Globalization;

namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// A published port. <paramref name="HostPort"/> is nullable to represent Compose's
/// <c>ports: ["8080"]</c> form, where Docker assigns the host port dynamically.
/// </summary>
/// <remarks>
/// A null host port means "published, host port chosen by Docker". It does not mean "not
/// published" — that state is an empty <see cref="ContainerService.Ports"/> collection, and it is
/// the correct configuration for a database that only the API needs to reach. Conflating the two
/// would push learners into publishing their database, which is the habit ComposeLab exists to
/// discourage.
/// </remarks>
public sealed record PortMapping(int? HostPort, int ContainerPort, PortProtocol Protocol = PortProtocol.Tcp)
{
    public PortPublishMode PublishMode => HostPort is null
        ? PortPublishMode.DynamicHostPort
        : PortPublishMode.ExplicitHostPort;

    /// <summary>The lowercase protocol token as Compose spells it.</summary>
    public string ProtocolToken => Protocol switch
    {
        PortProtocol.Udp => "udp",
        _ => "tcp"
    };

    /// <summary>
    /// Renders the mapping the way Compose writes it. The protocol suffix is omitted for TCP, which is
    /// Compose's default, and included for UDP.
    /// </summary>
    public string ToComposeSyntax()
    {
        string suffix = Protocol == PortProtocol.Udp ? "/udp" : string.Empty;

        return HostPort is null
            ? string.Create(CultureInfo.InvariantCulture, $"{ContainerPort}{suffix}")
            : string.Create(CultureInfo.InvariantCulture, $"{HostPort}:{ContainerPort}{suffix}");
    }
}
