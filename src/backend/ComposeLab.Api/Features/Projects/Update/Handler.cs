using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Domain.Errors;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComposeLab.Api.Features.Projects.Update;

/// <summary>
/// Replaces the saved name and topology.
/// </summary>
/// <remarks>
/// The schema version is checked before the entity is loaded, for the same reason as on read: loading would
/// deserialize an unknown document shape into the current one. A project written against an unreadable
/// contract is refused rather than silently overwritten, so nothing is lost on the basis of a guess.
/// </remarks>
internal sealed class Handler(AppDbContext dbContext, TimeProvider timeProvider) : ICommandHandler<Command>
{
    public async Task<Result> Handle(Command command, CancellationToken cancellationToken)
    {
        int? storedVersion = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == command.ProjectId)
            .Select(project => (int?)project.TopologySchemaVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedVersion is null)
        {
            return Result.Failure(ProjectErrors.NotFound(command.ProjectId));
        }

        if (storedVersion != TopologySchema.CurrentVersion)
        {
            return Result.Failure(ProjectErrors.UnsupportedTopologySchema(
                storedVersion.Value,
                TopologySchema.CurrentVersion));
        }

        Project project = await dbContext.Projects
            .SingleAsync(candidate => candidate.Id == command.ProjectId, cancellationToken);

        project.Replace(command.Name, command.Topology, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
