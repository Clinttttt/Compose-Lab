using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Topology;

public sealed class TopologyNormalizerTests
{
    [Fact]
    public void ServiceWithNoNetworks_JoinsTheImplicitDefaultNetwork()
    {
        ApplicationTopology topology = Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }]);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        normalized.Services.Single().Networks.ShouldHaveSingleItem();
        normalized.Services.Single().Networks[0].NetworkName.ShouldBe(TopologyNormalizer.DefaultNetworkName);
        normalized.Services.Single().Networks[0].Origin.ShouldBe(DeclarationOrigin.Implicit);
    }

    [Fact]
    public void DefaultNetworkIsDeclaredImplicitly_WhenNobodyDeclaredIt()
    {
        ApplicationTopology topology = Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }]);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        ContainerNetwork network = normalized.Networks.ShouldHaveSingleItem();
        network.Name.ShouldBe(TopologyNormalizer.DefaultNetworkName);
        network.Origin.ShouldBe(DeclarationOrigin.Implicit);
        normalized.DefaultNetworkWasMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void ExplicitlyDeclaredDefaultNetwork_KeepsItsExplicitOrigin()
    {
        // An author who configures `networks: default: ...` must keep that declaration on regeneration,
        // even though every service attaches to it implicitly.
        ApplicationTopology topology = Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }],
            networks: [TopologyNormalizer.DefaultNetworkName]);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        normalized.Networks.ShouldHaveSingleItem().Origin.ShouldBe(DeclarationOrigin.Explicit);
        normalized.Services.Single().Networks[0].Origin.ShouldBe(DeclarationOrigin.Implicit);
        normalized.DefaultNetworkWasMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void ExplicitAttachmentToAnUndeclaredDefault_MaterializesTheDeclarationOnly()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Networks = [Topologies.On(TopologyNormalizer.DefaultNetworkName)]
                }
            ]);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        normalized.Networks.ShouldHaveSingleItem().Origin.ShouldBe(DeclarationOrigin.Implicit);
        normalized.Services.Single().Networks[0].Origin.ShouldBe(DeclarationOrigin.Explicit);
    }

    [Fact]
    public void ServiceThatDeclaresNetworks_IsLeftAlone()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService { Name = "api", Image = "api:1", Networks = [Topologies.On("backend")] }
            ],
            networks: ["backend"]);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        normalized.Services.Single().Networks.ShouldHaveSingleItem().NetworkName.ShouldBe("backend");
        normalized.Services.Single().Networks[0].Origin.ShouldBe(DeclarationOrigin.Explicit);
        normalized.Networks.ShouldHaveSingleItem().Name.ShouldBe("backend");
        normalized.DefaultNetworkWasMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void MultiNetworkService_KeepsEveryAttachment()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "app",
                    Image = "app:1",
                    Networks = [Topologies.On("frontend"), Topologies.On("backend")]
                }
            ],
            networks: ["frontend", "backend"]);

        NormalizedTopology normalized = TopologyNormalizer.Normalize(topology);

        normalized.Services.Single().Networks
            .Select(attachment => attachment.NetworkName)
            .ShouldBe(["frontend", "backend"]);
    }

    [Fact]
    public void EmptyTopology_GetsNoDefaultNetwork()
    {
        NormalizedTopology normalized = TopologyNormalizer.Normalize(Topologies.With());

        normalized.Networks.ShouldBeEmpty();
        normalized.DefaultNetworkWasMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void Normalization_DoesNotMutateTheInput()
    {
        ApplicationTopology topology = Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }]);

        TopologyNormalizer.Normalize(topology);

        topology.Services.Single().Networks.ShouldBeEmpty();
        topology.Networks.ShouldBeEmpty();
    }
}
