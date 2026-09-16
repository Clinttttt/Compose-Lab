namespace ComposeLab.Api.Domain.Topology.Normalization;

/// <summary>
/// Makes Compose's implicit networking explicit so that every later rule can treat reachability as a
/// plain set intersection and still be correct.
/// </summary>
/// <remarks>
/// Docker's documented behavior: Compose sets up a single network for the project, every service
/// that does not declare <c>networks:</c> joins it, and services on a shared network are
/// discoverable by service name. A service that does declare <c>networks:</c> joins only those.
/// <para>
/// Two origins are tracked independently. The attachment origin records whether the author listed
/// the network on the service; the declaration origin records whether the author declared the
/// network at the top level. An author who configures <c>networks: default: ...</c> keeps an
/// explicit declaration even though every attachment to it is implicit, which is what lets
/// generation reproduce their file faithfully.
/// </para>
/// </remarks>
public static class TopologyNormalizer
{
    /// <summary>The network Compose creates for a project when a service declares none.</summary>
    public const string DefaultNetworkName = "default";

    public static NormalizedTopology Normalize(ApplicationTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        List<ContainerService> services = new(topology.Services.Count);
        bool defaultNetworkIsReferenced = false;

        foreach (ContainerService service in topology.Services)
        {
            if (service.Networks.Count == 0)
            {
                services.Add(service with
                {
                    Networks = [new NetworkAttachment(DefaultNetworkName, DeclarationOrigin.Implicit)]
                });

                defaultNetworkIsReferenced = true;
                continue;
            }

            services.Add(service);

            defaultNetworkIsReferenced |= service.Networks.Any(attachment =>
                attachment.NetworkName == DefaultNetworkName);
        }

        List<ContainerNetwork> networks = [.. topology.Networks];

        bool defaultNetworkIsDeclared = networks.Any(network => network.Name == DefaultNetworkName);

        if (defaultNetworkIsReferenced && !defaultNetworkIsDeclared)
        {
            networks.Add(new ContainerNetwork
            {
                Name = DefaultNetworkName,
                Origin = DeclarationOrigin.Implicit
            });
        }

        return new NormalizedTopology(services, networks, topology.Volumes);
    }
}
