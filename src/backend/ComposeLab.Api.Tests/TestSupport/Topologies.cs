using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Tests.TestSupport;

/// <summary>Terse construction of topologies, including deliberately broken ones.</summary>
internal static class Topologies
{
    public const string PostgresImage = "postgres:18";
    public const string PostgresDataPath = "/var/lib/postgresql/data";

    public static NetworkAttachment On(string network) => new(network, DeclarationOrigin.Explicit);

    public static ApplicationTopology With(
        IEnumerable<ContainerService>? services = null,
        IEnumerable<string>? networks = null,
        IEnumerable<string>? volumes = null) => new()
        {
            Services = [.. services ?? []],
            Networks = [.. (networks ?? []).Select(name => new ContainerNetwork { Name = name })],
            Volumes = [.. (volumes ?? []).Select(name => new ContainerVolume { Name = name })]
        };

    public static Dictionary<string, string> Env(params (string Key, string Value)[] variables) =>
        variables.ToDictionary(variable => variable.Key, variable => variable.Value, StringComparer.Ordinal);

    /// <summary>
    /// The architecture the graded walkthrough builds: an API published to the host, PostgreSQL kept
    /// internal with a named volume, both on one shared network, and a connection string that makes
    /// the intended communication explicit.
    /// </summary>
    public static ApplicationTopology ApiWithPostgres(
        string apiNetwork = "backend",
        string databaseNetwork = "backend") => With(
        services:
        [
            new ContainerService
            {
                Name = "api",
                BuildContext = "./Api",
                Ports = [new PortMapping(8080, 8080)],
                Networks = [On(apiNetwork)],
                Dependencies = [new ServiceDependency("database", DependencyCondition.ServiceStarted)],
                Environment = Env(("ConnectionStrings__Database", "Host=database;Database=app;Username=postgres"))
            },
            new ContainerService
            {
                Name = "database",
                Image = PostgresImage,
                Networks = [On(databaseNetwork)],
                Volumes = [new VolumeMount("postgres-data", PostgresDataPath)]
            }
        ],
        networks: new[] { apiNetwork, databaseNetwork }.Distinct(StringComparer.Ordinal),
        volumes: ["postgres-data"]);
}
