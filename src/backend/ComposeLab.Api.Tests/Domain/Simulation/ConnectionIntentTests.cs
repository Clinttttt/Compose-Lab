using ComposeLab.Api.Domain.Simulation;
using ComposeLab.Api.Domain.Topology;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Domain.Simulation;

/// <summary>
/// Intent inference is a heuristic, so these tests are mostly about what it must <em>not</em> claim.
/// </summary>
public sealed class ConnectionIntentTests
{
    /// <summary>
    /// The false-positive guard that makes the whole heuristic defensible. Only recognized host keys
    /// count, so <c>Database=app</c> and <c>Username=postgres</c> must not be read as intent to reach
    /// services named <c>app</c> and <c>postgres</c>.
    /// </summary>
    [Fact]
    public void OnlyHostKeysInAConnectionStringCount()
    {
        SimulationResult result = Simulate(
            ("ConnectionStrings__Database", "Host=database;Port=5432;Database=app;Username=postgres"),
            decoys: ["database", "app", "postgres"]);

        result.InferredConnections.ShouldHaveSingleItem().ToService.ShouldBe("database");
    }

    [Theory]
    [InlineData("Host=database", "database")]
    [InlineData("host=database;Port=5432", "database")]
    [InlineData("Server=database,1433;Database=app", "database")]
    [InlineData("Data Source=database;Initial Catalog=app", "database")]
    [InlineData("redis://database:6379", "database")]
    [InlineData("postgres://user:secret@database:5432/app", "database")]
    [InlineData("http://database:8080/health", "database")]
    [InlineData("amqp://guest:guest@database:5672/", "database")]
    public void ExplicitHostReferencesAreRecognized(string value, string expected)
    {
        SimulationResult result = Simulate(("SOME_SETTING", value), decoys: ["database"]);

        result.InferredConnections.ShouldHaveSingleItem().ToService.ShouldBe(expected);
    }

    /// <summary>
    /// A documented limitation: environment variable <em>names</em> are not inspected, so a bare
    /// <c>DB_HOST</c> yields nothing. Widening this is a deliberate later decision.
    /// </summary>
    [Fact]
    public void ABareHostnameValue_IsNotTreatedAsIntent()
    {
        SimulationResult result = Simulate(("DB_HOST", "database"), decoys: ["database"]);

        result.InferredConnections.ShouldBeEmpty();
    }

    [Fact]
    public void AHostThatIsNotAService_IsIgnored()
    {
        SimulationResult result = Simulate(
            ("ConnectionStrings__Database", "Host=some-external-host.example.com"),
            decoys: ["database"]);

        result.InferredConnections.ShouldBeEmpty();
    }

    [Fact]
    public void AReferenceToItself_IsIgnored()
    {
        SimulationResult result = Simulate(("SELF", "Host=api"), decoys: []);

        result.InferredConnections.ShouldBeEmpty();
    }

    [Fact]
    public void AnInferredConnectionRecordsWhereItCameFrom()
    {
        SimulationResult result = Simulate(
            ("ConnectionStrings__Database", "Host=database"),
            decoys: ["database"]);

        InferredConnection connection = result.InferredConnections.ShouldHaveSingleItem();

        connection.FromService.ShouldBe("api");
        connection.ToService.ShouldBe("database");
        connection.EnvironmentKey.ShouldBe("ConnectionStrings__Database");
        connection.Host.ShouldBe("database");
    }

    private static SimulationResult Simulate((string Key, string Value) variable, string[] decoys)
    {
        List<ContainerService> services =
        [
            new ContainerService
            {
                Name = "api",
                Image = "api:1",
                Environment = Topologies.Env(variable)
            },
            .. decoys.Select(name => new ContainerService { Name = name, Image = "busybox:1" })
        ];

        return TopologySimulator.Simulate(Topologies.With(services: services));
    }
}
