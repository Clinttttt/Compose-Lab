using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Features.Topology.ParseCompose;
using ComposeLab.Api.Tests.TestSupport;
using GeneratedComposeResponse = ComposeLab.Api.Features.Topology.GenerateCompose.Response;

namespace ComposeLab.Api.Tests.Features.Topology;

public sealed class ParseComposeTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Route = "/api/compose/parse";

    private readonly HttpClient _client = fixture.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AFileInTheSupportedSubsetComesBackAsATopology()
    {
        Response body = await Parse("""
            services:
              api:
                build: ./Api
                ports:
                  - "8080:8080"
                networks:
                  - backend
              database:
                image: postgres:18
                networks:
                  - backend

            networks:
              backend:
            """);

        body.CanApply.ShouldBeTrue();
        body.Findings.ShouldBeEmpty();
        body.Topology.ShouldNotBeNull();
        body.Topology.Services.Select(service => service.Name).ShouldBe(["api", "database"]);
        body.Topology.Services[0].Ports.ShouldHaveSingleItem().HostPort.ShouldBe(8080);
        body.Topology.Networks.ShouldHaveSingleItem().Name.ShouldBe("backend");
    }

    /// <summary>
    /// The all-or-nothing contract, end to end. One unmodeled key means no topology comes back at all, so
    /// the client has nothing it could partially apply.
    /// </summary>
    [Fact]
    public async Task OneUnmodeledKeyBlocksTheWholeFile()
    {
        Response body = await Parse("""
            services:
              api:
                image: api:1
                restart: unless-stopped
            """);

        body.CanApply.ShouldBeFalse();
        body.Topology.ShouldBeNull();

        ComposeFindingResponse finding = body.Findings.ShouldHaveSingleItem();

        finding.Code.ShouldBe("compose.key_not_modeled");
        finding.Path.ShouldBe("services.api.restart");
        finding.Line.ShouldBe(4);
        finding.Column.ShouldBeGreaterThan(0);
        finding.Message.ShouldNotBeNullOrWhiteSpace();
        finding.Suggestion.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ATypoIsReportedSeparatelyFromAnUnmodeledKey()
    {
        Response body = await Parse("""
            services:
              api:
                image: api:1
                enviroment:
                  KEY: value
            """);

        body.CanApply.ShouldBeFalse();
        body.Findings[0].Code.ShouldBe("compose.unknown_key");
    }

    /// <summary>A file ComposeLab cannot apply is a successful analysis, not a malformed request.</summary>
    [Fact]
    public async Task InvalidYamlIsStillATwoHundredWithAnExplanation()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            Route,
            new { yaml = "services:\n  api:\n    image: api:1\n    ports: [ \"8080\"\n" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Response? body = await response.Content.ReadFromJsonAsync<Response>(Token);

        body.ShouldNotBeNull();
        body.CanApply.ShouldBeFalse();
        body.Findings.ShouldContain(finding => finding.Code == "compose.syntax_error");
    }

    /// <summary>
    /// The parsed topology comes back in the same shape the client posts elsewhere, so applying a file is a
    /// swap rather than a translation. Feeding it straight to generate proves the contract lines up.
    /// </summary>
    [Fact]
    public async Task AParsedTopologyCanBePostedStraightBackToGenerate()
    {
        const string original = """
            services:
              api:
                image: api:1
                networks:
                  - backend

            networks:
              backend:

            """;

        Response parsed = await Parse(original);

        parsed.CanApply.ShouldBeTrue();

        HttpResponseMessage generated = await _client.PostAsJsonAsync(
            "/api/compose/generate",
            parsed.Topology,
            Token);

        generated.StatusCode.ShouldBe(HttpStatusCode.OK);

        GeneratedComposeResponse? body =
            await generated.Content.ReadFromJsonAsync<GeneratedComposeResponse>(Token);

        body.ShouldNotBeNull();
        body.Yaml.ShouldBe(original.ReplaceLineEndings("\n"));
    }

    private async Task<Response> Parse(string yaml)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(Route, new { yaml }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<Response>(Token)).ShouldNotBeNull();
    }
}
