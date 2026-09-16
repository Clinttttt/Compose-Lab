using ComposeLab.Api.Abstractions.Messaging;

namespace ComposeLab.Api.Features.Projects.GetById;

public sealed record Query(Guid ProjectId) : IQuery<Response>;
