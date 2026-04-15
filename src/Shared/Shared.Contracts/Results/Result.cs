namespace Shared.Contracts.Results;

/// <summary>
/// Result of an operation that does NOT return a value (equivalent to void).
/// Explicitly encapsulates success or failure.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        // Invariant: success cannot have an error, failure cannot be without error
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    /// <summary>Creates a success result without value.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>It creates a failure result.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>It creates a successful outcome with value.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>Creates a typed failure result (without a value).</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

}

/// <summary>
/// Result of an operation that returns a value of type T.
/// If IsSuccess, access Value. If IsFailure, access Error.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error? error) : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    // The value returned by the operation.
    // Throws an InvalidOperationException if accessed on a failure basis.
    // </summary>
    public TValue Value => IsSuccess ? _value! : 
        throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {Error.Code} - {Error.Message}");

    /// <summary>Implicit conversion: allows returning TValue directly as Result.</summary>
    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.Unexpected("Result.NullValue", "Value cannot be null."));
}