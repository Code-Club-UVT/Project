namespace CoreApi.Common.Results;

/// <summary>Outcome of an operation that can fail in an expected way. Unexpected failures are exceptions.</summary>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static Result<T> Success<T>(T value) => new(value);
    public static Result<T> Failure<T>(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value) : base(null)
    {
        _value = value;
    }

    internal Result(Error error) : base(error)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
