using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain;
using ComposeLab.Api.Domain.Common;
using ComposeLab.Api.Infrastructure.Persistence;

namespace ComposeLab.Api.Features.Projects.Create;

internal sealed class Handler(AppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<Command, Guid>
{
    public async Task<Result<Guid>> Handle(Command command, CancellationToken cancellationToken)
    {
        Project project = Project.Create(command.Name, command.Topology, timeProvider.GetUtcNow());

        dbContext.Projects.Add(project);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(project.Id);
    }
}
