using ComposeLab.Api.DependencyInjection;
using ComposeLab.Api.Extensions;
using ComposeLab.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddFrontendCors(builder.Configuration)
    .AddOpenApi()
    .AddProblemDetails()
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

// Before the endpoints, so preflight requests are answered rather than routed.
app.UseFrontendCors();

app.MapHealthChecks("/health");
app.MapEndpoints();

await app.RunAsync();

// WebApplicationFactory needs a reachable entry-point type.
public partial class Program;
