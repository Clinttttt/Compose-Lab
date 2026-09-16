using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Domain.Errors;
using ComposeLab.Api.Domain.Topology.Document;
using ComposeLab.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComposeLab.Api.Features.Projects.GetById;

/// <summary>
/// Returns one saved project.
/// </summary>
/// <remarks>
/// The schema version is checked with its own cheap query before the document is loaded. Reading first and
/// checking afterwards would mean deserializing an unknown shape into the current one, which is exactly the
/// silent reinterpretation this guard exists to prevent.
/// </remarks>
internal sealed class Handler(AppDbContext dbContext) : IQueryHandler<Query, Response>
{
    public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
    {
        int? storedVersion = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == query.ProjectId)
            .Select(project => (int?)project.TopologySchemaVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedVersion is null)
        {
            return Result.Failure<Response>(ProjectErrors.NotFound(query.ProjectId));
        }

        if (storedVersion != TopologySchema.CurrentVersion)
        {
            return Result.Failure<Response>(ProjectErrors.UnsupportedTopologySchema(
                storedVersion.Value,
                TopologySchema.CurrentVersion));
        }

        Project project = await dbContext.Projects
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == query.ProjectId, cancellationToken);

        return Result.Success(new Response
        {
            Id = project.Id,
            Name = project.Name,
            Topology = project.Topology,
            TopologySchemaVersion = project.TopologySchemaVersion,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        });
    }
}
