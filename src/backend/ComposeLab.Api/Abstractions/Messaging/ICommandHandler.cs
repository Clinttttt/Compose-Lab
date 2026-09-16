using ComposeLab.Api.Domain.Common;

namespace ComposeLab.Api.Abstractions.Messaging;

/// <summary>
/// Handles one write operation. Injected directly into its endpoint, so there is no dispatcher and no
/// reflection in the request path.
/// </summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
