using ComposeLab.Api.Domain.Compose;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Compose;

public sealed class ComposeParserTests
{
    /// <summary>
    /// The invariant that makes the YAML pane trustworthy: what ComposeLab writes, it can read back, and
    /// writing it again changes nothing. If this ever fails, applying an unedited file would silently
    /// rewrite it.
    /// </summary>
    [Fact]
    public void GeneratedComposeRoundTripsByteForByte()
    {
        string first = ComposeGenerator.Generate(Topologies.ApiWithPostgres()).Yaml;

        ComposeParseResult parsed = ComposeParser.Parse(first);

        parsed.CanApply.ShouldBeTrue();
        parsed.Findings.ShouldBeEmpty();

        ComposeGenerator.Generate(parsed.Topology!).Yaml.ShouldBe(first);
    }

    [Fact]
    public void AParsedTopologyCarriesTheArchitectureItDescribes()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                build: ./Api
                ports:
                  - "8080:8080"
                environment:
                  ConnectionStrings__Database: Host=database;Database=app
                networks:
                  - backend
                depends_on:
                  database:
                    condition: service_healthy

              database:
                image: postgres:18
                volumes:
                  - postgres-data:/var/lib/postgresql/data
                networks:
                  - backend
                healthcheck:
                  test: ["CMD", "pg_isready"]

            networks:
              backend:

            volumes:
              postgres-data:
            """);

        parsed.CanApply.ShouldBeTrue();

        ApplicationTopology topology = parsed.Topology!;

        topology.Services.Select(service => service.Name).ShouldBe(["api", "database"]);
        topology.Networks.ShouldHaveSingleItem().Name.ShouldBe("backend");
        topology.Volumes.ShouldHaveSingleItem().Name.ShouldBe("postgres-data");

        ContainerService api = topology.Services[0];
        api.BuildContext.ShouldBe("./Api");
        api.Ports.ShouldHaveSingleItem().ShouldBe(new PortMapping(8080, 8080));
        api.Environment["ConnectionStrings__Database"].ShouldBe("Host=database;Database=app");
        api.Networks.ShouldHaveSingleItem().NetworkName.ShouldBe("backend");
        api.Dependencies.ShouldHaveSingleItem().Condition.ShouldBe(DependencyCondition.ServiceHealthy);

        ContainerService database = topology.Services[1];
        database.Image.ShouldBe("postgres:18");
        database.Volumes.ShouldHaveSingleItem().ContainerPath.ShouldBe(Topologies.PostgresDataPath);
        database.HealthCheck!.Form.ShouldBe(HealthCheckTestForm.Command);
        database.HealthCheck.Arguments.ShouldBe(["pg_isready"]);
    }

    [Fact]
    public void ABlockedParseYieldsNoTopologyAtAll()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                restart: unless-stopped
            """);

        parsed.CanApply.ShouldBeFalse();
        parsed.Topology.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \n")]
    public void AnEmptyDocumentIsNotAppliedByAccident(string yaml)
    {
        ComposeParseResult parsed = ComposeParser.Parse(yaml);

        parsed.CanApply.ShouldBeFalse();
        Only(parsed).Message.ShouldContain("nothing to apply");
    }

    [Fact]
    public void InvalidYamlIsReportedAsASyntaxErrorWithAPosition()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
               ports:
            """);

        parsed.CanApply.ShouldBeFalse();

        ComposeFinding finding = Only(parsed);

        finding.Code.ShouldBe(ComposeFindingCode.SyntaxError);
        finding.Line.ShouldBeGreaterThan(1);
        finding.Suggestion.ShouldContain("indentation");
    }

    /// <summary>
    /// Real Compose that ComposeLab does not model. Reporting it separately from a typo is the point of the
    /// key catalog.
    /// </summary>
    [Theory]
    [InlineData("restart: unless-stopped", "services.api.restart")]
    [InlineData("command: dotnet App.dll", "services.api.command")]
    [InlineData("env_file: .env", "services.api.env_file")]
    [InlineData("container_name: my-api", "services.api.container_name")]
    [InlineData("volumes_from: other", "services.api.volumes_from")]
    [InlineData("network_mode: host", "services.api.network_mode")]
    [InlineData("x-my-extension: anything", "services.api.x-my-extension")]
    public void ValidComposeThatIsNotModeledBlocksAndSaysSo(string line, string path)
    {
        ComposeParseResult parsed = ComposeParser.Parse($"""
            services:
              api:
                image: api:1
                {line}
            """);

        ComposeFinding finding = Only(parsed);

        finding.Code.ShouldBe(ComposeFindingCode.KeyNotModeled);
        finding.Path.ShouldBe(path);
        finding.Message.ShouldContain("valid Compose");
        finding.Line.ShouldBe(4);
    }

    /// <summary>
    /// The cheapest teaching win available: catching a misspelling before Docker does. Collapsing this into
    /// one "unsupported" bucket would throw it away.
    /// </summary>
    [Theory]
    [InlineData("enviroment:\n      KEY: value")]
    [InlineData("network:\n      - backend")]
    [InlineData("port:\n      - \"8080\"")]
    public void AKeyThatIsNotComposeAtAllIsReportedAsATypo(string block)
    {
        ComposeParseResult parsed = ComposeParser.Parse($"""
            services:
              api:
                image: api:1
                {block}
            """);

        ComposeFinding finding = parsed.Findings[0];

        finding.Code.ShouldBe(ComposeFindingCode.UnknownKey);
        finding.Message.ShouldContain("typo");
    }

    [Fact]
    public void TheObsoleteVersionKeySaysItCanBeDeleted()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            version: "3.9"
            services:
              api:
                image: api:1
            """);

        ComposeFinding finding = Only(parsed);

        finding.Code.ShouldBe(ComposeFindingCode.KeyNotModeled);
        finding.Path.ShouldBe("version");
        finding.Suggestion.ShouldContain("obsolete");
    }

    [Theory]
    [InlineData("- \"3000-3005:3000\"")]
    [InlineData("- \"127.0.0.1:5000:8080\"")]
    [InlineData("- \"8080:8080/sctp\"")]
    [InlineData("- target: 8080\n        published: 8080")]
    public void APortFormThatIsNotModeledBlocks(string entry)
    {
        ComposeParseResult parsed = ComposeParser.Parse($"""
            services:
              api:
                image: api:1
                ports:
                  {entry}
            """);

        Only(parsed).Code.ShouldBe(ComposeFindingCode.ValueNotModeled);
    }

    [Theory]
    [InlineData("\"8080:8080\"", 8080, 8080, PortProtocol.Tcp)]
    [InlineData("\"8080\"", null, 8080, PortProtocol.Tcp)]
    [InlineData("8080", null, 8080, PortProtocol.Tcp)]
    [InlineData("\"5000:8080/udp\"", 5000, 8080, PortProtocol.Udp)]
    public void SupportedPortFormsAreRead(
        string entry,
        int? hostPort,
        int containerPort,
        PortProtocol protocol)
    {
        ComposeParseResult parsed = ComposeParser.Parse($"""
            services:
              api:
                image: api:1
                ports:
                  - {entry}
            """);

        parsed.CanApply.ShouldBeTrue();
        parsed.Topology!.Services[0].Ports.ShouldHaveSingleItem()
            .ShouldBe(new PortMapping(hostPort, containerPort, protocol));
    }

    [Fact]
    public void ABindMountIsExplainedRatherThanAccepted()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                volumes:
                  - ./data:/app/data
            """);

        ComposeFinding finding = Only(parsed);

        finding.Code.ShouldBe(ComposeFindingCode.ValueNotModeled);
        finding.Message.ShouldContain("bind mount");
        finding.Suggestion.ShouldContain("named volume");
    }

    [Fact]
    public void AnAccessModeOnAMountIsNotModeled()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                volumes:
                  - config:/etc/app:ro
            """);

        Only(parsed).Code.ShouldBe(ComposeFindingCode.ValueNotModeled);
    }

    [Fact]
    public void EnvironmentListFormIsRead()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                environment:
                  - ASPNETCORE_ENVIRONMENT=Development
                  - ConnectionStrings__Database=Host=database
            """);

        parsed.CanApply.ShouldBeTrue();

        IReadOnlyDictionary<string, string> environment = parsed.Topology!.Services[0].Environment;

        environment["ASPNETCORE_ENVIRONMENT"].ShouldBe("Development");
        environment["ConnectionStrings__Database"].ShouldBe("Host=database");
    }

    /// <summary>
    /// A variable with no value is taken from the machine running Docker, which cannot be reasoned about,
    /// so it is explained rather than quietly turned into an empty string.
    /// </summary>
    [Theory]
    [InlineData("environment:\n      - DATABASE_URL")]
    [InlineData("environment:\n      DATABASE_URL:")]
    public void AnEnvironmentVariableWithNoValueBlocks(string block)
    {
        ComposeParseResult parsed = ComposeParser.Parse($"""
            services:
              api:
                image: api:1
                {block}
            """);

        Only(parsed).Code.ShouldBe(ComposeFindingCode.ValueNotModeled);
    }

    [Fact]
    public void ShortFormDependenciesWaitForStart()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                depends_on:
                  - database
              database:
                image: postgres:18
            """);

        parsed.CanApply.ShouldBeTrue();
        parsed.Topology!.Services[0].Dependencies.ShouldHaveSingleItem()
            .ShouldBe(new ServiceDependency("database", DependencyCondition.ServiceStarted));
    }

    [Fact]
    public void AConditionThatIsNotModeledBlocks()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                depends_on:
                  database:
                    condition: service_completed_successfully
              database:
                image: postgres:18
            """);

        ComposeFinding finding = Only(parsed);

        finding.Code.ShouldBe(ComposeFindingCode.ValueNotModeled);
        finding.Path.ShouldBe("services.api.depends_on.database.condition");
    }

    [Fact]
    public void ADependencyOptionThatIsNotModeledBlocks()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                depends_on:
                  database:
                    condition: service_started
                    restart: true
              database:
                image: postgres:18
            """);

        Only(parsed).Code.ShouldBe(ComposeFindingCode.KeyNotModeled);
    }

    [Theory]
    [InlineData("test: pg_isready", HealthCheckTestForm.Shell, "pg_isready")]
    [InlineData("test: [\"CMD\", \"pg_isready\"]", HealthCheckTestForm.Command, "pg_isready")]
    [InlineData("test: [\"CMD-SHELL\", \"pg_isready\"]", HealthCheckTestForm.CommandShell, "pg_isready")]
    public void EachHealthCheckTestFormIsRecognizedSeparately(
        string line,
        HealthCheckTestForm expected,
        string firstArgument)
    {
        ComposeParseResult parsed = Parse($"""
                healthcheck:
                  {line}
            """);

        parsed.CanApply.ShouldBeTrue();

        HealthCheckDeclaration healthCheck = parsed.Topology!.Services[0].HealthCheck.ShouldNotBeNull();

        healthCheck.Form.ShouldBe(expected);
        healthCheck.Arguments[0].ShouldBe(firstArgument);
        healthCheck.IsEnabled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("disable: true")]
    [InlineData("test: NONE")]
    [InlineData("test: [\"NONE\"]")]
    public void EveryWayOfTurningAHealthCheckOffMeansTheSameThing(string line)
    {
        ComposeParseResult parsed = Parse($"""
                healthcheck:
                  {line}
            """);

        parsed.CanApply.ShouldBeTrue();

        HealthCheckDeclaration healthCheck = parsed.Topology!.Services[0].HealthCheck.ShouldNotBeNull();

        healthCheck.Form.ShouldBe(HealthCheckTestForm.Disabled);
        healthCheck.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void HealthCheckTimingOptionsAreNotModeled()
    {
        ComposeParseResult parsed = Parse("""
                healthcheck:
                  test: pg_isready
                  interval: 10s
                  retries: 3
            """);

        parsed.CanApply.ShouldBeFalse();
        parsed.Findings.Count.ShouldBe(2);
        parsed.Findings.ShouldAllBe(finding => finding.Code == ComposeFindingCode.KeyNotModeled);
    }

    [Fact]
    public void AHealthCheckWithNothingToRunBlocks()
    {
        ComposeParseResult parsed = Parse("""
                healthcheck:
                  disable: false
            """);

        Only(parsed).Code.ShouldBe(ComposeFindingCode.ValueNotModeled);
    }

    [Fact]
    public void ANetworkNameAttributeIsRead()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
                networks:
                  - backend
            networks:
              backend:
                name: shared-backend
            """);

        parsed.CanApply.ShouldBeTrue();

        ContainerNetwork network = parsed.Topology!.Networks.ShouldHaveSingleItem();

        network.Name.ShouldBe("backend");
        network.ComposeName.ShouldBe("shared-backend");
    }

    [Fact]
    public void ANetworkNameAttributeSurvivesRegeneration()
    {
        const string yaml = """
            networks:
              backend:
                name: shared-backend

            """;

        ComposeParseResult parsed = ComposeParser.Parse(yaml);

        parsed.CanApply.ShouldBeTrue();
        ComposeGenerator.Generate(parsed.Topology!).Yaml.ShouldBe(yaml.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ANetworkOptionThatIsNotModeledBlocks()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            networks:
              backend:
                driver: bridge
            """);

        ComposeFinding finding = Only(parsed);

        finding.Code.ShouldBe(ComposeFindingCode.KeyNotModeled);
        finding.Path.ShouldBe("networks.backend.driver");
    }

    [Fact]
    public void AWrongShapeIsReportedRatherThanCrashing()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              - api
            """);

        Only(parsed).Code.ShouldBe(ComposeFindingCode.UnexpectedShape);
    }

    [Fact]
    public void ADocumentThatIsNotAMappingIsReported()
    {
        ComposeParseResult parsed = ComposeParser.Parse("- just a list");

        Only(parsed).Code.ShouldBe(ComposeFindingCode.UnexpectedShape);
    }

    /// <summary>
    /// Every problem is reported in one pass. Returning only the first would turn fixing a file into
    /// whack-a-mole.
    /// </summary>
    [Fact]
    public void EveryProblemIsReportedTogether()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            version: "3.9"
            services:
              api:
                image: api:1
                restart: always
                enviroment:
                  KEY: value
            """);

        parsed.CanApply.ShouldBeFalse();
        parsed.Findings.Count.ShouldBe(3);

        parsed.Findings.Select(finding => finding.Code.Value).ShouldBe(
            [
                "compose.key_not_modeled",
                "compose.key_not_modeled",
                "compose.unknown_key"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void ServicesWithNoNetworksParseCleanlyAndLeaveNormalizationToDoItsJob()
    {
        ComposeParseResult parsed = ComposeParser.Parse("""
            services:
              api:
                image: api:1
              database:
                image: postgres:18
            """);

        parsed.CanApply.ShouldBeTrue();
        parsed.Topology!.Networks.ShouldBeEmpty();
        parsed.Topology.Services.ShouldAllBe(service => service.Networks.Count == 0);
    }

    private static ComposeParseResult Parse(string serviceBlock) => ComposeParser.Parse($"""
        services:
          database:
            image: postgres:18
        {serviceBlock}
        """);

    private static ComposeFinding Only(ComposeParseResult parsed)
    {
        parsed.CanApply.ShouldBeFalse();

        return parsed.Findings.ShouldHaveSingleItem();
    }
}
