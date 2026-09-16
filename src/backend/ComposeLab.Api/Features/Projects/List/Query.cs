using ComposeLab.Api.Abstractions.Messaging;

namespace ComposeLab.Api.Features.Projects.List;

/// <summary>Lists saved projects.</summary>
public sealed record Query : IQuery<Response>;
