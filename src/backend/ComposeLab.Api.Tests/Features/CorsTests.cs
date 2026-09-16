using System.Net;
using System.Net.Http.Json;
using ComposeLab.Api.Tests.TestSupport;

namespace ComposeLab.Api.Tests.Features;

/// <summary>
/// The frontend runs on its own dev server, so every call it makes is cross-origin. These tests pin both
/// halves of that: the configured origin works, and nothing else does.
/// </summary>
public sealed class CorsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string DevOrigin = "http://localhost:4200";
    private const string UnknownOrigin = "http://not-composelab.example";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private readonly HttpClient _client = fixture.CreateClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APreflightFromTheFrontendIsAnswered()
    {
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/topology/simulate");
        request.Headers.Add("Origin", DevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        HttpResponseMessage response = await _client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues(AllowOriginHeader).ShouldContain(DevOrigin);
        response.Headers.GetValues("Access-Control-Allow-Methods").ShouldContain(value =>
            value.Contains("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARealRequestFromTheFrontendCarriesTheAllowHeader()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/topology/validate")
        {
            Content = JsonContent.Create(new { services = Array.Empty<object>() })
        };

        request.Headers.Add("Origin", DevOrigin);

        HttpResponseMessage response = await _client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues(AllowOriginHeader).ShouldContain(DevOrigin);
    }

    [Fact]
    public async Task AnOriginThatIsNotConfiguredGetsNoAllowHeader()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/topology/validate")
        {
            Content = JsonContent.Create(new { services = Array.Empty<object>() })
        };

        request.Headers.Add("Origin", UnknownOrigin);

        HttpResponseMessage response = await _client.SendAsync(request, Token);

        // The browser is what enforces this: no allow header means it refuses to hand the body to the caller.
        response.Headers.Contains(AllowOriginHeader).ShouldBeFalse();
    }

    /// <summary>
    /// Credentials stay off. There is nothing to send yet, and having enabled it is how a loosened origin list
    /// later turns into a real vulnerability rather than a misconfiguration.
    /// </summary>
    [Fact]
    public async Task CredentialsAreNeverAllowed()
    {
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/projects");
        request.Headers.Add("Origin", DevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await _client.SendAsync(request, Token);

        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    [Fact]
    public async Task TheWildcardOriginIsNeverUsed()
    {
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/topology/simulate");
        request.Headers.Add("Origin", DevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        HttpResponseMessage response = await _client.SendAsync(request, Token);

        response.Headers.GetValues(AllowOriginHeader).ShouldNotContain("*");
    }
}
