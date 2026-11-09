namespace BuildingBlocks.Abstractions;

/// <summary>Classifies an expected failure so the web layer can map it to a precise HTTP status.</summary>
public enum ErrorType { Failure, Validation, NotFound, Conflict, Forbidden, Unauthorized, Unprocessable }

/// <summary>
/// A railway-oriented error. Handlers return these instead of throwing for expected failures.
/// <paramref name="Field"/> optionally names the input property the error is about (e.g. "email"), so the web
/// layer can report it under ProblemDetails <c>errors</c> and a client can show it next to that field.
/// </summary>
public readonly record struct Error(string Code, string Message, ErrorType Type, string? Field = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string msg)                         => new("failure", msg, ErrorType.Failure);
    public static Error Validation(string msg, string? field = null) => new("validation", msg, ErrorType.Validation, field);
    public static Error NotFound(string msg)                        => new("not_found", msg, ErrorType.NotFound);
    public static Error Conflict(string msg, string? field = null)  => new("conflict", msg, ErrorType.Conflict, field);
    public static Error Forbidden(string msg)                       => new("forbidden", msg, ErrorType.Forbidden);
    public static Error Unauthorized(string msg)                    => new("unauthorized", msg, ErrorType.Unauthorized);
    public static Error Unprocessable(string msg)                   => new("unprocessable", msg, ErrorType.Unprocessable);
}

/// <summary>Result of an operation that can fail in an expected way (no exception thrown).</summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    protected Result(bool ok, Error? error)
    {
        if (ok && error is not null && error.Value != Abstractions.Error.None)
            throw new InvalidOperationException("A successful result cannot carry an error.");
        IsSuccess = ok;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(Error e) => new(false, e);
    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(Error e) => new(default!, false, e);

    public static implicit operator Result(Error e) => Failure(e);
}

/// <summary>Result carrying a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T _value;

    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    internal Result(T value, bool ok, Error? error) : base(ok, error) => _value = value;

    /// <summary>Ergonomic success: <c>return dto;</c></summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Ergonomic failure: <c>return Error.NotFound("...");</c></summary>
    public static implicit operator Result<T>(Error e) => Failure<T>(e);
}
