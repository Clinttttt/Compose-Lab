using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Extensions;

namespace ComposeLab.Api.Features.Projects.List;

internal sealed class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/projects", async (
                IQueryHandler<Query, Response> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler.Handle(new Query(), cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("ListProjects")
            .WithTags("Projects")
            .Produces<Response>();
}
