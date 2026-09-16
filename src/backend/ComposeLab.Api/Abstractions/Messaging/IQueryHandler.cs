using ComposeLab.Api.Domain.Common;

namespace ComposeLab.Api.Abstractions.Messaging;

/// <summary>
/// Handles one query. Injected directly into its endpoint, so there is no dispatcher and no
/// reflection in the request path.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
