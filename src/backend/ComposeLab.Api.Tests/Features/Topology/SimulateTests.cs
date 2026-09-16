using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Features.Topology.Shared;
using ComposeLab.Api.Features.Topology.Simulate;
using ComposeLab.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Tests.Features.Topology;

/// <summary>
/// Exercises the real pipeline: routing, model binding, the validation filter, endpoint discovery, and
/// Problem Details.
/// </summary>
public sealed class SimulateTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Route = "/api/topology/simulate";

    private readonly HttpClient _client = fixture.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AWorkingArchitectureReturnsOkAndSucceeded()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, WorkingTopology(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.Succeeded.ShouldBeTrue();
        body.Completed.ShouldBeTrue();
        body.StartOrder.ShouldBe(["database", "api"]);
        body.Events.ShouldNotBeEmpty();
        body.Reachability.ShouldHaveSingleItem().CanCommunicate.ShouldBeTrue();
        body.InferredConnections.ShouldHaveSingleItem().ToService.ShouldBe("database");
    }

    /// <summary>
    /// A broken architecture is a successful request. Returning 4xx would conflate "your request was
    /// malformed" with "your architecture has a problem", and the second is what the endpoint is for.
    /// </summary>
    [Fact]
    public async Task ABrokenArchitectureReturnsOkWithAnExplanation()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, BrokenNetworkTopology(), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.Succeeded.ShouldBeFalse();
        body.Completed.ShouldBeTrue();

        ArchitectureIssueResponse issue = body.Issues.Single(item => item.Code == "network.unreachable");

        issue.Severity.ShouldBe("error");
        issue.WhatHappened.ShouldNotBeNullOrWhiteSpace();
        issue.Why.ShouldNotBeNullOrWhiteSpace();
        issue.ArchitectureBehavior.ShouldNotBeNullOrWhiteSpace();
        issue.SuggestedFix.ShouldNotBeNullOrWhiteSpace();
        issue.Elements.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AMalformedRequestReturnsValidationProblemNamingTheField()
    {
        object payload = new
        {
            services = new[]
            {
                new
                {
                    name = "api",
                    image = "api:1",
                    ports = new[] { new { containerPort = 0 } }
                }
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
    /// End-to-end proof that the validator does not steal a teaching case: duplicate service names
    /// reach the simulator and come back as an explanation, not a 400.
    /// </summary>
    [Fact]
    public async Task AWrongArchitectureIsNotRejectedByValidation()
    {
        object payload = new
        {
            services = new[]
            {
                new { name = "api", image = "api:1" },
                new { name = "api", image = "api:2" }
            }
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.Succeeded.ShouldBeFalse();
        body.Completed.ShouldBeFalse();
        body.Issues.ShouldContain(issue => issue.Code == "structure.duplicate_service_name");
    }

    /// <summary>Pins the wire contract the frontend renders from.</summary>
    [Fact]
    public async Task CodesPhasesAndSeveritiesAreSerializedAsStableStrings()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, BrokenNetworkTopology(), Token);

        string json = await response.Content.ReadAsStringAsync(Token);

        json.ShouldContain("\"code\":\"network.unreachable\"");
        json.ShouldContain("\"severity\":\"error\"");
        json.ShouldContain("\"phase\":\"normalization\"");
        json.ShouldContain("\"kind\":\"environment_variable\"");
        json.ShouldContain("\"architectureBehavior\"");
    }

    [Fact]
    public async Task TheValidationFilterIsAttachedToTheEndpoint()
    {
        // An endpoint missing .AddEndpointFilter<ValidationFilter<Query>>() would answer 200 here.
        object payload = new { services = new[] { new { name = string.Empty, image = "api:1" } } };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static object WorkingTopology() => new
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

    private static object BrokenNetworkTopology() => new
    {
        services = new object[]
        {
            new
            {
                name = "api",
                build = "./Api",
                networks = new[] { "frontend" },
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
        networks = new[] { new { name = "frontend" }, new { name = "backend" } },
        volumes = new[] { new { name = "postgres-data" } }
    };
}
