using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Build.Results;

/// <summary>
/// Boring typed result for expected operation success/failure. Use the
/// <see cref="Success"/> / <see cref="Failure"/> factories to construct; access
/// <see cref="Value"/> or <see cref="Error"/> only when the corresponding state is
/// known (the off-state accessor throws <see cref="InvalidOperationException"/>).
/// Use <see cref="TryGetValue"/> / <see cref="TryGetError"/> for soft probes when
/// state is uncertain. No implicit conversions, no Map/Bind.
/// </summary>
/// <remarks>
/// <c>default(Result)</c> is a failure result with <c>default(TError)</c>; production
/// code should not construct default instances — use <see cref="Success"/> or
/// <see cref="Failure"/>.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification =
        "Factory methods on a generic value type match the .NET BCL pattern (ImmutableArray<T>.Create, Nullable<T> helpers). The non-generic alternative would force Result.Failure<int, string>(\"err\") at every call site, where Result<int, string>.Failure(\"err\") reads cleaner.")]
public readonly record struct Result<T, TError>
{
    private readonly T _value;
    private readonly TError _error;

    private Result(T value)
    {
        _value = value;
        _error = default!;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        _value = default!;
        _error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value =>
        IsSuccess
            ? _value
            : throw new InvalidOperationException("Result is in failure state; access Error or use TryGetValue.");

    public TError Error =>
        !IsSuccess
            ? _error
            : throw new InvalidOperationException("Result is in success state; access Value or use TryGetError.");

    public static Result<T, TError> Success(T value) => new(value);

    public static Result<T, TError> Failure(TError error) => new(error);

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return IsSuccess;
    }

    public bool TryGetError([MaybeNullWhen(false)] out TError error)
    {
        error = _error;
        return !IsSuccess;
    }
}
