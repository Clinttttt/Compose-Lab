using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Behaviors;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Features.Topology.GenerateCompose;

/// <summary>
/// Generates canonical Compose YAML from an architecture supplied in the request body.
/// </summary>
/// <remarks>
/// Unauthenticated, like the other engine routes in this phase: stateless, reads nothing, and bounded
/// by the request validator's collection limits. That has to be revisited once anything is persisted or
/// the API becomes publicly reachable.
/// </remarks>
internal sealed class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/compose/generate", async (
                [FromBody] Query query,
                IQueryHandler<Query, Response> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler.Handle(query, cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("GenerateCompose")
            .WithTags("Compose")
            .AddEndpointFilter<ValidationFilter<Query>>()
            .Produces<Response>()
            .ProducesValidationProblem();
}
