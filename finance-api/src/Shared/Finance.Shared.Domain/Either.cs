namespace Finance.Shared.Domain;

public sealed class Either<TFailure, TSuccess>
{
    private readonly TFailure? failure;
    private readonly TSuccess? success;

    private Either(TFailure failure)
    {
        this.failure = failure;
        IsFailure = true;
    }

    private Either(TSuccess success)
    {
        this.success = success;
        IsFailure = false;
    }

    public bool IsFailure { get; }

    public bool IsSuccessful => !IsFailure;

    public TFailure Failure => IsFailure ? failure! : throw new InvalidOperationException("A successful result has no failure.");

    public TSuccess Success => IsSuccessful ? success! : throw new InvalidOperationException("A failed result has no success value.");

    public static Either<TFailure, TSuccess> FromFailure(TFailure failure)
    {
        return new Either<TFailure, TSuccess>(failure);
    }

    public static Either<TFailure, TSuccess> FromSuccess(TSuccess success)
    {
        return new Either<TFailure, TSuccess>(success);
    }

    public static implicit operator Either<TFailure, TSuccess>(TFailure failure)
    {
        return FromFailure(failure);
    }

    public static implicit operator Either<TFailure, TSuccess>(TSuccess success)
    {
        return FromSuccess(success);
    }
}
