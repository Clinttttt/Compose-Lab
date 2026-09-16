using Microsoft.Net.Http.Headers;

namespace ComposeLab.Api.DependencyInjection;

/// <summary>
/// Cross-origin access for the frontend, which runs on its own dev server and is therefore a different origin
/// from the API.
/// </summary>
/// <remarks>
/// Three deliberate choices. Origins come from configuration as an explicit allowlist, never
/// <c>AllowAnyOrigin</c>. Credentials are not allowed, because there is nothing to send yet and enabling it
/// now is how a permissive origin later becomes a real vulnerability. And when no origins are configured the
/// middleware is not added at all, so anything other than a developer machine has no cross-origin access
/// until someone says otherwise — the default is closed rather than accidentally open.
/// </remarks>
public static class CorsServices
{
    public const string PolicyName = "ComposeLabFrontend";

    private const string AllowedOriginsPath = "Cors:AllowedOrigins";

    public static string[] AllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection(AllowedOriginsPath).Get<string[]>() ?? [];

    public static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string[] origins = AllowedOrigins(configuration);

        if (origins.Length == 0)
        {
            return services;
        }

        services.AddCors(options => options.AddPolicy(
            PolicyName,
            policy => policy
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put)
                .WithHeaders(HeaderNames.ContentType)));

        return services;
    }

    public static WebApplication UseFrontendCors(this WebApplication app)
    {
        if (AllowedOrigins(app.Configuration).Length > 0)
        {
            app.UseCors(PolicyName);
        }

        return app;
    }
}
