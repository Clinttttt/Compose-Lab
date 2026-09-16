using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using GetProjectResponse = ComposeLab.Api.Features.Projects.GetById.Response;
using ListProjectsResponse = ComposeLab.Api.Features.Projects.List.Response;
using ValidateResponse = ComposeLab.Api.Features.Topology.Validate.Response;

namespace ComposeLab.Api.Tests.Features.Projects;

[Trait("Category", "Integration")]
public sealed class ProjectsTests(DatabaseFixture fixture)
    : DatabaseTestBase(fixture), IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task ANewProjectIsCreatedAndAddressable()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/projects", NewProject("Web API"), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        Guid id = await response.Content.ReadFromJsonAsync<Guid>(Token);

        id.ShouldNotBe(Guid.Empty);
        response.Headers.Location!.ToString().ShouldEndWith(id.ToString());
        (await Context.Projects.CountAsync(Token)).ShouldBe(1);
    }

    [Fact]
    public async Task TheSavedTopologyComesBackExactly()
    {
        Guid id = await Create(NewProject("Web API"));

        GetProjectResponse project = await Get(id);

        project.Name.ShouldBe("Web API");
        project.TopologySchemaVersion.ShouldBe(TopologySchema.CurrentVersion);

        project.Topology.Services.Select(service => service.Name).ShouldBe(["api", "database"]);
        project.Topology.Networks.ShouldHaveSingleItem().Name.ShouldBe("backend");
        project.Topology.Volumes.ShouldHaveSingleItem().Name.ShouldBe("postgres-data");

        ServiceDocument api = project.Topology.Services[0];
        api.Build.ShouldBe("./Api");
        api.Ports.ShouldHaveSingleItem().HostPort.ShouldBe(8080);
        api.Ports[0].ContainerPort.ShouldBe(8080);
        api.Environment["ConnectionStrings__Database"].ShouldBe("Host=database;Database=app");
        api.DependsOn.ShouldHaveSingleItem().Condition.ShouldBe("service_healthy");

        ServiceDocument database = project.Topology.Services[1];
        database.Image.ShouldBe("postgres:18");
        database.Volumes.ShouldHaveSingleItem().Path.ShouldBe("/var/lib/postgresql/data");
        database.HealthCheck!.Form.ShouldBe("command");
        database.HealthCheck.Test.ShouldBe(["pg_isready"]);
    }

    /// <summary>
    /// The rule that makes saving useful. Duplicate names and a colliding host port are exactly the mistakes
    /// ComposeLab exists to let a learner build, inspect, and come back to. Saving is held to the malformed
    /// input boundary and no further.
    /// </summary>
    [Fact]
    public async Task AStructurallyBrokenArchitectureIsSaveable()
    {
        object broken = new
        {
            name = "Broken on purpose",
            topology = new
            {
                services = new object[]
                {
                    new
                    {
                        name = "api",
                        image = "api:1",
                        ports = new[] { new { hostPort = 8080, containerPort = 8080 } },
                        networks = new[] { "nowhere" }
                    },
                    new
                    {
                        name = "api",
                        image = "api:2",
                        ports = new[] { new { hostPort = 8080, containerPort = 9090 } }
                    }
                }
            }
        };

        Guid id = await Create(broken);

        GetProjectResponse project = await Get(id);

        project.Topology.Services.Count.ShouldBe(2);
        project.Topology.Services.ShouldAllBe(service => service.Name == "api");

        // And the engine still calls it broken, which is the point of keeping the two separate.
        HttpResponseMessage validated = await Client.PostAsJsonAsync(
            "/api/topology/validate",
            project.Topology,
            Token);

        validated.StatusCode.ShouldBe(HttpStatusCode.OK);

        ValidateResponse? report = await validated.Content.ReadFromJsonAsync<ValidateResponse>(Token);

        report.ShouldNotBeNull();
        report.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task AMalformedTopologyIsStillRejected()
    {
        object malformed = new
        {
            name = "Bad envelope",
            topology = new
            {
                services = new[]
                {
                    new { name = "api", image = "api:1", ports = new[] { new { containerPort = 0 } } }
                }
            }
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/projects", malformed, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Context.Projects.CountAsync(Token)).ShouldBe(0);
    }

    /// <summary>
    /// Compose's implied network is a normalization result. Saving and reopening a project must not turn it
    /// into configuration the learner appears to have written.
    /// </summary>
    [Fact]
    public async Task SavingAndReopeningDoesNotInventImpliedNetworking()
    {
        Guid id = await Create(new
        {
            name = "No networks",
            topology = new
            {
                services = new[] { new { name = "api", image = "api:1" } }
            }
        });

        GetProjectResponse project = await Get(id);

        project.Topology.Networks.ShouldBeEmpty();
        project.Topology.Services.ShouldHaveSingleItem().Networks.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListingReturnsSummariesWithoutAnyTopology()
    {
        await Create(NewProject("First"));
        await Create(NewProject("Second"));

        HttpResponseMessage response = await Client.GetAsync("/api/projects", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync(Token);

        // Quoted so that "topologySchemaVersion" does not count as the topology being present.
        json.ShouldNotContain("\"services\"");
        json.ShouldNotContain("\"topology\"");

        ListProjectsResponse? body = await response.Content.ReadFromJsonAsync<ListProjectsResponse>(Token);

        body.ShouldNotBeNull();
        body.Projects.Count.ShouldBe(2);
        body.Projects.Select(project => project.Name).ShouldContain("First");
        body.Projects.ShouldAllBe(project => project.TopologySchemaVersion == 1);
    }

    [Fact]
    public async Task AnUnknownProjectIsNotFound()
    {
        HttpResponseMessage response = await Client.GetAsync($"/api/projects/{Guid.CreateVersion7()}", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SavingReplacesTheTopologyWholesale()
    {
        Guid id = await Create(NewProject("Web API"));

        GetProjectResponse before = await Get(id);

        HttpResponseMessage updated = await Client.PutAsJsonAsync(
            $"/api/projects/{id}",
            new
            {
                name = "Web API and cache",
                topology = new
                {
                    services = new[] { new { name = "cache", image = "redis:8" } }
                }
            },
            Token);

        updated.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        GetProjectResponse after = await Get(id);

        after.Name.ShouldBe("Web API and cache");
        after.Topology.Services.ShouldHaveSingleItem().Name.ShouldBe("cache");
        after.Topology.Networks.ShouldBeEmpty();
        after.Topology.Volumes.ShouldBeEmpty();
        after.CreatedAt.ShouldBe(before.CreatedAt);
        after.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before.UpdatedAt);
    }

    [Fact]
    public async Task SavingAnUnknownProjectIsNotFound()
    {
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            $"/api/projects/{Guid.CreateVersion7()}",
            NewProject("Nowhere"),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The stored value has to be real jsonb in the shape the client sees, not an opaque blob or a
    /// .NET-flavoured encoding. Reading it back through Postgres itself is the only way to know.
    /// </summary>
    [Fact]
    public async Task TheTopologyIsStoredAsQueryableJsonInTheWireShape()
    {
        Guid id = await Create(NewProject("Web API"));

        string firstServiceName = await ScalarAsync(
            $"SELECT \"Topology\" -> 'services' -> 0 ->> 'name' FROM projects WHERE \"Id\" = '{id}'");

        firstServiceName.ShouldBe("api");

        string typeName = await ScalarAsync(
            $"SELECT pg_typeof(\"Topology\")::text FROM projects WHERE \"Id\" = '{id}'");

        typeName.ShouldBe("jsonb");
    }

    /// <summary>
    /// A document written against a contract this build does not know must be refused, not guessed at. Reading
    /// it as though it were current is how a learner's architecture would quietly change shape.
    /// </summary>
    [Fact]
    public async Task ADocumentFromAnUnknownSchemaVersionIsRefused()
    {
        Guid id = Guid.CreateVersion7();

        await Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO projects (\"Id\", \"Name\", \"Topology\", \"TopologySchemaVersion\", \"CreatedAt\", "
                + "\"UpdatedAt\") VALUES ({0}, {1}, {2}::jsonb, {3}, {4}, {5})",
            [id, "From the future", "{\"services\":[]}", 999, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow],
            Token);

        HttpResponseMessage read = await Client.GetAsync($"/api/projects/{id}", Token);

        read.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await read.Content.ReadAsStringAsync(Token)).ShouldContain("project.unsupported_topology_schema");

        HttpResponseMessage written = await Client.PutAsJsonAsync(
            $"/api/projects/{id}",
            NewProject("Overwrite attempt"),
            Token);

        written.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AProjectNameIsRequired()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/projects",
            new { name = string.Empty, topology = new { services = Array.Empty<object>() } },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> Create(object payload)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/projects", payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<Guid>(Token);
    }

    private async Task<GetProjectResponse> Get(Guid id)
    {
        HttpResponseMessage response = await Client.GetAsync($"/api/projects/{id}", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<GetProjectResponse>(Token)).ShouldNotBeNull();
    }

    private async Task<string> ScalarAsync(string sql)
    {
        await using Npgsql.NpgsqlConnection connection = new(Fixture.ConnectionString);
        await connection.OpenAsync(Token);

        await using Npgsql.NpgsqlCommand command = new(sql, connection);

        return (await command.ExecuteScalarAsync(Token))?.ToString() ?? string.Empty;
    }

    private static object NewProject(string name) => new
    {
        name,
        topology = new
        {
            services = new object[]
            {
                new
                {
                    name = "api",
                    build = "./Api",
                    ports = new[] { new { hostPort = 8080, containerPort = 8080 } },
                    networks = new[] { "backend" },
                    dependsOn = new[] { new { service = "database", condition = "service_healthy" } },
                    environment = new Dictionary<string, string>
                    {
                        ["ConnectionStrings__Database"] = "Host=database;Database=app"
                    }
                },
                new
                {
                    name = "database",
                    image = "postgres:18",
                    networks = new[] { "backend" },
                    volumes = new[] { new { volume = "postgres-data", path = "/var/lib/postgresql/data" } },
                    healthCheck = new { form = "command", test = new[] { "pg_isready" } }
                }
            },
            networks = new[] { new { name = "backend" } },
            volumes = new[] { new { name = "postgres-data" } }
        }
    };
}
