namespace Shared.Contracts.Results;

/// <summary>
/// Maps directly to HTTP status codes on endpoints.
/// </summary>
public enum ErrorType
{
    None = 0,
    NotFound = 404,
    Validation = 400,
    BadRequest = 400,
    Conflict = 409,
    Forbidden = 403,
    Unexpected = 500
}

/// <summary>
/// Represents a typed, structured error.
/// Immutable record — each error is a value, not an exception.
/// </summary>
public sealed record Error
{
    private Error(string code, string message, ErrorType type)
    {
        Code = code;
        Message = message;
        Type = type;
    }

    public string Code { get; init; }
    public string Message { get; init; }
    public ErrorType Type { get; init; }
    
    // Factory methods - Typed constructors for different error types

    /// <summary>Resource not found → 404</summary>
    public static Error NotFound(string code, string message) 
        => new(code, message, ErrorType.NotFound);
    
    /// <summary>Validation Error → 400</summary>
    public static Error Validation(string code, string message = "") =>
        new(code, message, ErrorType.Validation);
    

    /// <summary>Invalid request due to business rule → 400</summary>
    public static Error BadRequest(string code, string message = "") => 
        new(code, message, ErrorType.BadRequest);


    /// <summary>Conflict (ex: resource already exists) → 409</summary>
    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);


    /// <summary>Without permission → 403</summary>
    public static Error Forbidden(string code, string message) =>
        new(code, message, ErrorType.Forbidden);


    /// <summary>Generic error when no specific type applies → 500</summary>
    public static Error Unexpected(string code, string message) =>
        new(code, message, ErrorType.Unexpected);


    /// <summary>No error — used internally by Result.Success. NEVER use directly.</summary>
    internal static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

}
