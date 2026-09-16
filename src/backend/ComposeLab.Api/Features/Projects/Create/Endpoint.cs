using ComposeLab.Api.Abstractions.Endpoints;
using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Behaviors;
using ComposeLab.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ComposeLab.Api.Features.Projects.Create;

/// <summary>
/// Saves a new project.
/// </summary>
/// <remarks>
/// Unauthenticated, and this is the route where that stops being merely theoretical: from here on the API
/// holds learner work, so anyone who can reach it can create and overwrite projects. Acceptable for local and
/// classroom use, and the thing to fix before this is reachable from anywhere else.
/// </remarks>
internal sealed class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/projects", async (
                [FromBody] Command command,
                ICommandHandler<Command, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Guid> result = await handler.Handle(command, cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"/api/projects/{result.Value}", result.Value)
                    : Extensions.ResultExtensions.ToHttpResult(result);
            })
            .WithName("CreateProject")
            .WithTags("Projects")
            .AddEndpointFilter<ValidationFilter<Command>>()
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
}
