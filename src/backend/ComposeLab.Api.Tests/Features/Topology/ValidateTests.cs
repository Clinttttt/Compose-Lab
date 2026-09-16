using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Features.Topology.Shared;
using ComposeLab.Api.Features.Topology.Validate;
using ComposeLab.Api.Tests.TestSupport;
using SimulateResponse = ComposeLab.Api.Features.Topology.Simulate.Response;

namespace ComposeLab.Api.Tests.Features.Topology;

public sealed class ValidateTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Route = "/api/topology/validate";

    private readonly HttpClient _client = fixture.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASoundArchitectureIsValid()
    {
        Response body = await Validate(Working());

        body.IsValid.ShouldBeTrue();
        body.IsComplete.ShouldBeTrue();
        body.Issues.ShouldNotContain(issue => issue.Severity == "error");
    }

    [Fact]
    public async Task ABrokenConnectionIsReportedWithItsExplanation()
    {
        Response body = await Validate(Broken());

        body.IsValid.ShouldBeFalse();
        body.IsComplete.ShouldBeTrue();

        ArchitectureIssueResponse issue =
            body.Issues.Single(item => item.Code == "network.unreachable");

        issue.Severity.ShouldBe("error");
        issue.WhatHappened.ShouldNotBeNullOrWhiteSpace();
        issue.Why.ShouldNotBeNullOrWhiteSpace();
        issue.ArchitectureBehavior.ShouldNotBeNullOrWhiteSpace();
        issue.SuggestedFix.ShouldNotBeNullOrWhiteSpace();
        issue.Elements.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Validation answers only "what is wrong". The ordered story of what would happen belongs to the
    /// simulate route, and keeping them apart is what makes this one cheap enough to run after every edit.
    /// </summary>
    [Fact]
    public async Task ValidationCarriesNoTimeline()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, Working(), Token);

        string json = await response.Content.ReadAsStringAsync(Token);

        json.ShouldNotContain("\"events\"");
        json.ShouldNotContain("\"startOrder\"");
        json.ShouldNotContain("\"reachability\"");
    }

    /// <summary>
    /// The same architecture, both routes, one answer. If these ever diverge, ComposeLab is teaching two
    /// different things about the same file.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateAndSimulateAgreeOverHttp(bool sound)
    {
        object payload = sound ? Working() : Broken();

        Response validated = await Validate(payload);

        HttpResponseMessage simulated = await _client.PostAsJsonAsync(
            "/api/topology/simulate",
            payload,
            Token);

        SimulateResponse? simulation = await simulated.Content.ReadFromJsonAsync<SimulateResponse>(Token);

        simulation.ShouldNotBeNull();
        validated.IsValid.ShouldBe(simulation.Succeeded);
        validated.IsComplete.ShouldBe(simulation.Completed);

        validated.Issues.Select(issue => issue.Code).Order()
            .ShouldBe(simulation.Issues.Select(issue => issue.Code).Order());
    }

    [Fact]
    public async Task StructuralFaultsMarkTheReportIncomplete()
    {
        object payload = new
        {
            services = new[]
            {
                new { name = "api", image = "api:1" },
                new { name = "api", image = "api:2" }
            }
        };

        Response body = await Validate(payload);

        body.IsValid.ShouldBeFalse();
        body.IsComplete.ShouldBeFalse();
        body.Issues.ShouldContain(issue => issue.Code == "structure.duplicate_service_name");
    }

    [Fact]
    public async Task AMalformedRequestReturnsValidationProblem()
    {
        object payload = new { services = new[] { new { name = string.Empty, image = "api:1" } } };

        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Response> Validate(object payload)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, payload, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<Response>(Token)).ShouldNotBeNull();
    }

    private static object Working() => Topology("backend", "backend");

    private static object Broken() => Topology("frontend", "backend");

    private static object Topology(string apiNetwork, string databaseNetwork) => new
    {
        services = new object[]
        {
            new
            {
                name = "api",
                build = "./Api",
                ports = new[] { new { hostPort = 8080, containerPort = 8080 } },
                networks = new[] { apiNetwork },
                environment = new Dictionary<string, string>
                {
                    ["ConnectionStrings__Database"] = "Host=database;Database=app"
                }
            },
            new
            {
                name = "database",
                image = "postgres:18",
                networks = new[] { databaseNetwork },
                volumes = new[] { new { volume = "postgres-data", path = "/var/lib/postgresql/data" } }
            }
        },
        networks = new[] { apiNetwork, databaseNetwork }
            .Distinct(StringComparer.Ordinal)
            .Select(name => new { name })
            .ToArray(),
        volumes = new[] { new { name = "postgres-data" } }
    };
}
