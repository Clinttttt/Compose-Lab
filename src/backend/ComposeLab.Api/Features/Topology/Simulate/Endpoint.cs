using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Behaviors;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Features.Topology.Simulate;

/// <summary>
/// Simulates an architecture supplied in the request body.
/// </summary>
/// <remarks>
/// There is no authorization on this route. The endpoint is stateless and reads nothing, so the risk
/// is bounded to compute, which the validator's collection limits cap. That is acceptable for local
/// and classroom use and is not acceptable once anything is persisted or the API is publicly
/// reachable — the project slices that introduce persistence are where that has to be revisited.
/// </remarks>
internal sealed class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/topology/simulate", async (
                [FromBody] Query query,
                IQueryHandler<Query, Response> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler.Handle(query, cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("SimulateTopology")
            .WithTags("Topology")
            .AddEndpointFilter<ValidationFilter<Query>>()
            .Produces<Response>()
            .ProducesValidationProblem();
}
