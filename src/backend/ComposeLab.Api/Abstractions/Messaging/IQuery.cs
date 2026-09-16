namespace ComposeLab.Api.Abstractions.Messaging;

/// <summary>A read operation that returns <typeparamref name="TResponse"/>.</summary>
/// <remarks>
/// The command side of this pair — <c>ICommand</c> and <c>ICommandHandler</c> — is deliberately
/// absent until the first write slice exists. An interface with no implementation is a contract
/// nothing has agreed to.
/// </remarks>
public interface IQuery<TResponse>;
