using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Domain.Topology.Normalization;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Topology;

public sealed class TopologyDocumentTests
{
    /// <summary>
    /// The constraint that keeps saving honest: Compose's implied network is a normalization result, not user
    /// configuration. If it leaked into the document, saving and reopening a project would silently turn
    /// something ComposeLab explains into something the learner appears to have written.
    /// </summary>
    [Fact]
    public void ANormalizedTopologyDoesNotLeakImpliedNetworkingIntoTheDocument()
    {
        NormalizedTopology normalized = TopologyNormalizer.Normalize(Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }]));

        normalized.DefaultNetworkWasMaterialized.ShouldBeTrue();

        TopologyDocument document = TopologyDocumentMapper.ToDocument(new ApplicationTopology
        {
            Services = normalized.Services,
            Networks = normalized.Networks,
            Volumes = normalized.Volumes
        });

        document.Networks.ShouldBeEmpty();
        document.Services.ShouldHaveSingleItem().Networks.ShouldBeEmpty();
    }

    [Fact]
    public void AnAuthoredArchitectureSurvivesARoundTripThroughTheDocument()
    {
        TopologyDocument original = TopologyDocumentMapper.ToDocument(Topologies.ApiWithPostgres());

        TopologyDocument round = TopologyDocumentMapper.ToDocument(
            TopologyDocumentMapper.ToTopology(original));

        round.Services.Select(service => service.Name).ShouldBe(["api", "database"]);
        round.Networks.Select(network => network.Name).ShouldBe(["backend"]);
        round.Volumes.Select(volume => volume.Name).ShouldBe(["postgres-data"]);

        ServiceDocument api = round.Services[0];
        api.Build.ShouldBe("./Api");
        api.Ports.ShouldHaveSingleItem().HostPort.ShouldBe(8080);
        api.Ports[0].Protocol.ShouldBe("tcp");
        api.Networks.ShouldBe(["backend"]);
        api.DependsOn.ShouldHaveSingleItem().Condition.ShouldBe("service_started");
        api.Environment.ShouldContainKey("ConnectionStrings__Database");

        ServiceDocument database = round.Services[1];
        database.Image.ShouldBe("postgres:18");
        database.Volumes.ShouldHaveSingleItem().Path.ShouldBe(Topologies.PostgresDataPath);
    }

    [Theory]
    [InlineData(HealthCheckTestForm.Disabled, "disabled")]
    [InlineData(HealthCheckTestForm.Shell, "shell")]
    [InlineData(HealthCheckTestForm.Command, "command")]
    [InlineData(HealthCheckTestForm.CommandShell, "command_shell")]
    public void EveryHealthCheckFormSurvivesTheDocument(HealthCheckTestForm form, string token)
    {
        HealthCheckDeclaration declaration = form switch
        {
            HealthCheckTestForm.Shell => HealthCheckDeclaration.Shell("pg_isready"),
            HealthCheckTestForm.Command => HealthCheckDeclaration.Command(["pg_isready"]),
            HealthCheckTestForm.CommandShell => HealthCheckDeclaration.CommandShell("pg_isready"),
            _ => HealthCheckDeclaration.Disabled
        };

        TopologyDocument document = TopologyDocumentMapper.ToDocument(Topologies.With(
            services:
            [
                new ContainerService { Name = "database", Image = "postgres:18", HealthCheck = declaration }
            ]));

        document.Services[0].HealthCheck!.Form.ShouldBe(token);

        TopologyDocumentMapper.ToTopology(document).Services[0].HealthCheck!.Form.ShouldBe(form);
    }

    [Fact]
    public void TheSchemaVersionIsRecordedFromTheFirstRelease() =>
        TopologySchema.CurrentVersion.ShouldBe(1);
}
