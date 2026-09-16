namespace ComposeLab.Api.Abstractions.Messaging;

/// <summary>A write operation that returns no value.</summary>
public interface ICommand;

/// <summary>A write operation that returns <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse>;
