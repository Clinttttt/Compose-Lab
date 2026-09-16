using ComposeLab.Api.Domain.Compose;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Domain.Topology.Normalization;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Compose;

public sealed class ComposeGeneratorTests
{
    /// <summary>
    /// Pins the canonical output exactly. Everything downstream — the YAML pane, the correspondence
    /// highlighting, and eventually round-trip parsing — is measured against this shape.
    /// </summary>
    [Fact]
    public void TheReferenceArchitectureProducesCanonicalCompose()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.ApiWithPostgres());

        const string expected = """
            services:
              api:
                build: ./Api
                ports:
                  - "8080:8080"
                environment:
                  ConnectionStrings__Database: Host=database;Database=app;Username=postgres
                networks:
                  - backend
                depends_on:
                  - database

              database:
                image: postgres:18
                volumes:
                  - postgres-data:/var/lib/postgresql/data
                networks:
                  - backend

            networks:
              backend:

            volumes:
              postgres-data:

            """;

        document.Yaml.ShouldBe(expected.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void EveryElementIsMappedToTheLinesItProduced()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.ApiWithPostgres());

        Range(ElementReference.Service("api")).ShouldBe(new YamlRange(2, 11));
        Range(ElementReference.Service("database")).ShouldBe(new YamlRange(13, 18));

        Range(ElementReference.Port("api", new PortMapping(8080, 8080))).ShouldBe(new YamlRange(5, 5));
        Range(ElementReference.EnvironmentVariable("api", "ConnectionStrings__Database"))
            .ShouldBe(new YamlRange(7, 7));
        Range(ElementReference.NetworkAttachment("api", "backend")).ShouldBe(new YamlRange(9, 9));
        Range(ElementReference.Dependency("api", "database")).ShouldBe(new YamlRange(11, 11));

        Range(ElementReference.VolumeMount(
                "database",
                new VolumeMount("postgres-data", Topologies.PostgresDataPath)))
            .ShouldBe(new YamlRange(16, 16));
        Range(ElementReference.NetworkAttachment("database", "backend")).ShouldBe(new YamlRange(18, 18));

        Range(ElementReference.Network("backend")).ShouldBe(new YamlRange(21, 21));
        Range(ElementReference.Volume("postgres-data")).ShouldBe(new YamlRange(24, 24));

        YamlRange Range(ElementReference element) => document.RangeOf(element).ShouldNotBeNull();
    }

    /// <summary>
    /// A declaration and a use are different elements even when they name the same resource, which is
    /// what lets the inspector highlight the top-level network separately from one service's membership
    /// of it.
    /// </summary>
    [Fact]
    public void ADeclarationAndAnAttachmentAreDistinctElements()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.ApiWithPostgres());

        document.RangeOf(ElementReference.Network("backend"))
            .ShouldNotBe(document.RangeOf(ElementReference.NetworkAttachment("api", "backend")));
    }

    [Fact]
    public void EveryProvenanceRangeLandsInsideTheDocument()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.ApiWithPostgres());

        int lines = document.Yaml.TrimEnd('\n').Split('\n').Length;

        document.Provenance.ShouldNotBeEmpty();

        foreach (ProvenanceEntry entry in document.Provenance)
        {
            entry.Range.StartLine.ShouldBeGreaterThanOrEqualTo(1);
            entry.Range.EndLine.ShouldBeLessThanOrEqualTo(lines);
            entry.Range.EndLine.ShouldBeGreaterThanOrEqualTo(entry.Range.StartLine);
        }
    }

    /// <summary>
    /// An implied network is Compose's behavior, not the author's configuration. Writing it out would put
    /// words in their mouth and would make the file disagree with what they wrote.
    /// </summary>
    [Fact]
    public void ImpliedNetworkingIsNeverWrittenOut()
    {
        NormalizedTopology normalized = TopologyNormalizer.Normalize(Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }]));

        ApplicationTopology topology = new()
        {
            Services = normalized.Services,
            Networks = normalized.Networks,
            Volumes = normalized.Volumes
        };

        ComposeDocument document = ComposeGenerator.Generate(topology);

        document.Yaml.ShouldNotContain("networks");
        document.Yaml.ShouldNotContain("default");
        document.Yaml.ShouldBe("services:\n  api:\n    image: api:1\n");
    }

    /// <summary>
    /// An explicitly configured top-level default survives, even though every attachment to it is
    /// implicit. That asymmetry is the whole reason the two origins are tracked separately.
    /// </summary>
    [Fact]
    public void AnExplicitlyDeclaredDefaultNetworkSurvives()
    {
        NormalizedTopology normalized = TopologyNormalizer.Normalize(Topologies.With(
            services: [new ContainerService { Name = "api", Image = "api:1" }],
            networks: [TopologyNormalizer.DefaultNetworkName]));

        ApplicationTopology topology = new()
        {
            Services = normalized.Services,
            Networks = normalized.Networks,
            Volumes = normalized.Volumes
        };

        ComposeDocument document = ComposeGenerator.Generate(topology);

        document.Yaml.ShouldContain("networks:\n  default:");
        document.Yaml.ShouldNotContain("- default");
    }

    /// <summary>
    /// Under YAML 1.1, which Compose parsers accept, an unquoted 8080:8080 is a base-60 number. Port
    /// mappings are therefore always quoted.
    /// </summary>
    [Theory]
    [InlineData(8080, 8080, PortProtocol.Tcp, "\"8080:8080\"")]
    [InlineData(null, 8080, PortProtocol.Tcp, "\"8080\"")]
    [InlineData(8080, 53, PortProtocol.Udp, "\"8080:53/udp\"")]
    [InlineData(null, 53, PortProtocol.Udp, "\"53/udp\"")]
    public void PortMappingsAreAlwaysQuotedAndOmitTheDefaultProtocol(
        int? hostPort,
        int containerPort,
        PortProtocol protocol,
        string expected)
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Ports = [new PortMapping(hostPort, containerPort, protocol)]
                }
            ]));

        document.Yaml.ShouldContain($"- {expected}");
    }

    [Theory]
    [InlineData("Development", "Development")]
    [InlineData("Host=database;Database=app", "Host=database;Database=app")]
    [InlineData("5432", "\"5432\"")]
    [InlineData("true", "\"true\"")]
    [InlineData("no", "\"no\"")]
    [InlineData("", "\"\"")]
    [InlineData(" padded ", "\" padded \"")]
    [InlineData("*star", "\"*star\"")]
    [InlineData("key: value", "\"key: value\"")]
    [InlineData("trailing:", "\"trailing:\"")]
    [InlineData("1.0.0", "\"1.0.0\"")]
    public void EnvironmentValuesAreQuotedOnlyWhenTheyHaveTo(string value, string expected)
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Environment = Topologies.Env(("SETTING", value))
                }
            ]));

        document.Yaml.ShouldContain($"SETTING: {expected}");
    }

    [Fact]
    public void AnImageTagIsNotMistakenForAMappingKey()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services: [new ContainerService { Name = "database", Image = "postgres:18-alpine" }]));

        document.Yaml.ShouldContain("image: postgres:18-alpine");
    }

    [Fact]
    public void EnvironmentVariablesAreWrittenInAStableOrder()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Environment = Topologies.Env(("ZONE", "a"), ("ALPHA", "b"), ("MIDDLE", "c"))
                }
            ]));

        document.Yaml.ShouldContain("ALPHA: b\n      MIDDLE: c\n      ZONE: a");
    }

    [Fact]
    public void DependenciesUseTheShortFormWhenNoConditionIsNeeded()
    {
        ComposeDocument document = ComposeGenerator.Generate(WithDependency(DependencyCondition.ServiceStarted));

        document.Yaml.ShouldContain("depends_on:\n      - database");
        document.Yaml.ShouldNotContain("condition");
    }

    /// <summary>
    /// The short list form cannot carry a condition, so one health condition moves every dependency of
    /// that service into the long form.
    /// </summary>
    [Fact]
    public void OneHealthConditionMovesTheWholeServiceToTheLongForm()
    {
        ApplicationTopology topology = Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    Dependencies =
                    [
                        new ServiceDependency("database", DependencyCondition.ServiceHealthy),
                        new ServiceDependency("cache", DependencyCondition.ServiceStarted)
                    ]
                }
            ]);

        ComposeDocument document = ComposeGenerator.Generate(topology);

        document.Yaml.ShouldContain("""
                depends_on:
                  database:
                    condition: service_healthy
                  cache:
                    condition: service_started
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ALongFormDependencySpansBothOfItsLines()
    {
        ComposeDocument document = ComposeGenerator.Generate(WithDependency(DependencyCondition.ServiceHealthy));

        YamlRange range = document.RangeOf(ElementReference.Dependency("api", "database")).ShouldNotBeNull();

        range.LineCount.ShouldBe(2);
    }

    [Fact]
    public void ADisabledHealthCheckIsWrittenAsDisable()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services:
            [
                new ContainerService
                {
                    Name = "api",
                    Image = "api:1",
                    HealthCheck = HealthCheckDeclaration.Disabled
                }
            ]));

        document.Yaml.ShouldContain("healthcheck:\n      disable: true");
    }

    [Fact]
    public void AnExecFormHealthCheckKeepsItsCmdPrefix()
    {
        ComposeDocument document = ComposeGenerator.Generate(WithHealthCheck(
            HealthCheckDeclaration.Command(["pg_isready", "-U", "postgres"])));

        // "-U" is quoted because a plain scalar may not start with a YAML indicator.
        document.Yaml.ShouldContain("""
                healthcheck:
                  test:
                    - CMD
                    - pg_isready
                    - "-U"
                    - postgres
            """.ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// Compose runs a scalar test through a shell and a CMD list directly, so the two forms are written
    /// back out the way they were written down.
    /// </summary>
    [Fact]
    public void AScalarHealthCheckStaysAScalar()
    {
        ComposeDocument document = ComposeGenerator.Generate(WithHealthCheck(
            HealthCheckDeclaration.Shell("pg_isready -U postgres")));

        document.Yaml.ShouldContain("healthcheck:\n      test: pg_isready -U postgres");
    }

    [Fact]
    public void AnExplicitShellHealthCheckKeepsItsCmdShellPrefix()
    {
        ComposeDocument document = ComposeGenerator.Generate(WithHealthCheck(
            HealthCheckDeclaration.CommandShell("pg_isready || exit 1")));

        document.Yaml.ShouldContain("""
                healthcheck:
                  test:
                    - CMD-SHELL
                    - pg_isready || exit 1
            """.ReplaceLineEndings("\n"));
    }

    private static ApplicationTopology WithHealthCheck(HealthCheckDeclaration healthCheck) => Topologies.With(
        services:
        [
            new ContainerService
            {
                Name = "database",
                Image = "postgres:18",
                HealthCheck = healthCheck
            }
        ]);

    /// <summary>
    /// Modeling the test form semantically means every healthcheck this type can hold has a valid Compose
    /// form to write, so "declared but with nothing to run" is no longer expressible.
    /// </summary>
    [Fact]
    public void EveryHealthCheckFormCanBeWrittenBackOut()
    {
        HealthCheckDeclaration[] forms =
        [
            HealthCheckDeclaration.Disabled,
            HealthCheckDeclaration.Shell("pg_isready"),
            HealthCheckDeclaration.Command(["pg_isready"]),
            HealthCheckDeclaration.CommandShell("pg_isready")
        ];

        foreach (HealthCheckDeclaration form in forms)
        {
            ComposeGenerator.Generate(WithHealthCheck(form)).Yaml.ShouldContain("healthcheck:");
        }
    }

    [Fact]
    public void AnUnpublishedServiceWritesNoPortsSection()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services: [new ContainerService { Name = "database", Image = "postgres:18" }]));

        document.Yaml.ShouldNotContain("ports");
    }

    [Fact]
    public void AnEmptyTopologyProducesAnEmptyDocument()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With());

        document.Yaml.ShouldBeEmpty();
        document.Provenance.ShouldBeEmpty();
    }

    [Fact]
    public void GenerationIsDeterministic()
    {
        ApplicationTopology topology = Topologies.ApiWithPostgres();

        ComposeGenerator.Generate(topology).Yaml.ShouldBe(ComposeGenerator.Generate(topology).Yaml);
    }

    /// <summary>
    /// Generation projects, it does not judge. A duplicate service name produces duplicate keys, because
    /// telling the learner what is wrong is the simulator's job and doing it in two places would let the
    /// two disagree.
    /// </summary>
    [Fact]
    public void GenerationDoesNotValidate()
    {
        ComposeDocument document = ComposeGenerator.Generate(Topologies.With(
            services:
            [
                new ContainerService { Name = "api", Image = "api:1" },
                new ContainerService { Name = "api", Image = "api:2" }
            ]));

        document.Yaml.ShouldContain("image: api:1");
        document.Yaml.ShouldContain("image: api:2");
    }

    private static ApplicationTopology WithDependency(DependencyCondition condition) => Topologies.With(
        services:
        [
            new ContainerService
            {
                Name = "api",
                Image = "api:1",
                Dependencies = [new ServiceDependency("database", condition)]
            }
        ]);
}
