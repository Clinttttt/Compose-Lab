using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComposeLab.Api.Features.Projects.List;

internal sealed class Handler(AppDbContext dbContext) : IQueryHandler<Query, Response>
{
    public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
    {
        // Projecting straight into the summary keeps the jsonb column out of the query entirely.
        List<ProjectSummaryResponse> projects = await dbContext.Projects
            .AsNoTracking()
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Id)
            .Select(project => new ProjectSummaryResponse(
                project.Id,
                project.Name,
                project.TopologySchemaVersion,
                project.CreatedAt,
                project.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new Response { Projects = projects });
    }
}
