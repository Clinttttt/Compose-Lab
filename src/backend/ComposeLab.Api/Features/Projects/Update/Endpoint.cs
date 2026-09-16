using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Behaviors;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Features.Projects.Update;

/// <summary>
/// Saves the working topology into an existing project.
/// </summary>
/// <remarks>
/// Unauthenticated in this phase, which means anyone who can reach the API can overwrite any project. That is
/// acceptable for local and classroom use and is the first thing to change before this is reachable from
/// anywhere else.
/// </remarks>
internal sealed class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/api/projects/{projectId:guid}", async (
                Guid projectId,
                [FromBody] Command command,
                ICommandHandler<Command> handler,
                CancellationToken cancellationToken) =>
            {
                Result result = await handler.Handle(command with { ProjectId = projectId }, cancellationToken);

                return result.ToHttpResult();
            })
            .WithName("UpdateProject")
            .WithTags("Projects")
            .AddEndpointFilter<ValidationFilter<Command>>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
