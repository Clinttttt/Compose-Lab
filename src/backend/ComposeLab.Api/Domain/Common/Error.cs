namespace ComposeLab.Api.Domain.Common;

/// <summary>
/// The category of an expected failure. Deliberately transport-agnostic: the mapping from a
/// category to an HTTP status code lives in <c>Extensions/ResultExtensions.cs</c> and nowhere else.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict
}

/// <summary>
/// An expected failure, identified by a stable code. Codes come from catalogs under
/// <c>Domain/Errors</c> so that every code the API can return is greppable in one place.
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);
}
