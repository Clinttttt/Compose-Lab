using ComposeLab.Api.DependencyInjection;
using ComposeLab.Api.Extensions;
using ComposeLab.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
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

app.MapHealthChecks("/health");
app.MapEndpoints();

await app.RunAsync();

// WebApplicationFactory needs a reachable entry-point type.
public partial class Program;
