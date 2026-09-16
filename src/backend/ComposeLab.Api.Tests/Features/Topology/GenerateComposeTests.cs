using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Features.Topology.GenerateCompose;
using ComposeLab.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Tests.Features.Topology;

public sealed class GenerateComposeTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Route = "/api/compose/generate";

    private readonly HttpClient _client = fixture.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnArchitectureIsReturnedAsComposeYaml()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, ReferenceTopology(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.Yaml.ShouldContain("services:");
        body.Yaml.ShouldContain("image: postgres:18");
        body.Yaml.ShouldContain("- \"8080:8080\"");
        body.Yaml.ShouldContain("volumes:\n  postgres-data:");
        body.Provenance.ShouldNotBeEmpty();
    }

    /// <summary>
    /// The contract that carries the visual-to-YAML correspondence. The frontend matches an element from
    /// a simulation issue against these entries, so both sides must speak the same element language.
    /// </summary>
    [Fact]
    public async Task ProvenanceIdentifiesElementsTheSameWayIssuesDo()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, ReferenceTopology(), Token);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();

        string[] lines = body.Yaml.TrimEnd('\n').Split('\n');

        ProvenanceResponse api = body.Provenance.Single(entry =>
            entry.Element.Kind == "service" && entry.Element.Name == "api");

        lines[api.StartLine - 1].ShouldBe("  api:");
        api.EndLine.ShouldBeGreaterThan(api.StartLine);

        ProvenanceResponse environment = body.Provenance.Single(entry =>
            entry.Element.Kind == "environment_variable");

        environment.Element.OwnerService.ShouldBe("api");
        environment.Element.Name.ShouldBe("ConnectionStrings__Database");
        lines[environment.StartLine - 1].ShouldContain("ConnectionStrings__Database");

        // A top-level declaration has no owner; one service's attachment to it does.
        body.Provenance.ShouldContain(entry =>
            entry.Element.Kind == "network" && entry.Element.Name == "backend"
            && entry.Element.OwnerService == null);

        body.Provenance.ShouldContain(entry =>
            entry.Element.Kind == "network" && entry.Element.Name == "backend"
            && entry.Element.OwnerService == "api");
    }

    [Fact]
    public async Task AMalformedRequestReturnsValidationProblem()
    {
        object payload = new
        {
            services = new[]
            {
                new { name = "api", image = "api:1", ports = new[] { new { containerPort = 70000 } } }
            }
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        ValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Token);

        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Services[0].Ports[0].ContainerPort");
    }

    /// <summary>
    /// A healthcheck must name a form ComposeLab understands and carry what that form needs, which is what
    /// guarantees every accepted healthcheck can be written back out.
    /// </summary>
    [Fact]
    public async Task AHealthCheckFormThatIsNotRecognizedIsRejected()
    {
        object payload = new
        {
            services = new[]
            {
                new
                {
                    name = "database",
                    image = "postgres:18",
                    healthCheck = new { form = "sometimes", test = new[] { "pg_isready" } }
                }
            }
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        ValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Token);

        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain("Services[0].HealthCheck.Form");
    }

    [Fact]
    public async Task TheCommandFormNeedsAtLeastOneArgument()
    {
        object payload = new
        {
            services = new[]
            {
                new
                {
                    name = "database",
                    image = "postgres:18",
                    healthCheck = new { form = "command", test = Array.Empty<string>() }
                }
            }
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        ValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Token);

        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain("Services[0].HealthCheck.Test");
    }

    [Fact]
    public async Task ADisabledHealthCheckNeedsNoTestCommand()
    {
        object payload = new
        {
            services = new[]
            {
                new
                {
                    name = "database",
                    image = "postgres:18",
                    healthCheck = new { form = "disabled", test = Array.Empty<string>() }
                }
            }
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.Yaml.ShouldContain("disable: true");
    }

    [Fact]
    public async Task AScalarHealthCheckIsWrittenAsAScalar()
    {
        object payload = new
        {
            services = new[]
            {
                new
                {
                    name = "database",
                    image = "postgres:18",
                    healthCheck = new { form = "shell", test = new[] { "pg_isready -U postgres" } }
                }
            }
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.Yaml.ShouldContain("test: pg_isready -U postgres");
    }

    /// <summary>
    /// The same request body shape serves every engine route, which is what keeps the frontend from
    /// maintaining a second topology contract.
    /// </summary>
    [Fact]
    public async Task TheSameRequestBodyWorksForSimulateAndGenerate()
    {
        HttpResponseMessage generated = await _client.PostAsJsonAsync(Route, ReferenceTopology(), Token);
        HttpResponseMessage simulated = await _client.PostAsJsonAsync(
            "/api/topology/simulate",
            ReferenceTopology(),
            Token);

        generated.StatusCode.ShouldBe(HttpStatusCode.OK);
        simulated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static object ReferenceTopology() => new
    {
        services = new object[]
        {
            new
            {
                name = "api",
                build = "./Api",
                ports = new[] { new { hostPort = 8080, containerPort = 8080 } },
                networks = new[] { "backend" },
                dependsOn = new[] { new { service = "database", condition = "service_started" } },
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
                volumes = new[] { new { volume = "postgres-data", path = "/var/lib/postgresql/data" } }
            }
        },
        networks = new[] { new { name = "backend" } },
        volumes = new[] { new { name = "postgres-data" } }
    };
}
