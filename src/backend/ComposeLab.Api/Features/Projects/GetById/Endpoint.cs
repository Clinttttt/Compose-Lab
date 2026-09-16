using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Extensions;

namespace ComposeLab.Api.Features.Projects.GetById;

internal sealed class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects/{projectId:guid}", async (
                Guid projectId,
                IQueryHandler<Query, Response> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler.Handle(new Query(projectId), cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("GetProjectById")
            .WithTags("Projects")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
