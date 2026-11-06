namespace TeamPortal.Application.Common;

/// <summary>One thing wrong with one field, ready to show under that field.</summary>
/// <param name="Field">
/// The request property name, e.g. <c>nameof(CreateEmployeeRequest.FullName)</c>.
/// Use <see cref="ValidationError.FormLevel"/> for errors that belong to no single field.
/// </param>
/// <param name="Message">Plain English, shown to the user as written.</param>
public sealed record ValidationError(string Field, string Message)
{
    /// <summary>Field name for an error about the form as a whole.</summary>
    public const string FormLevel = "";
}

/// <summary>The outcome of a write that returns nothing.</summary>
public class Result
{
    private static readonly IReadOnlyList<ValidationError> NoErrors = [];

    protected Result(bool isSuccess, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<ValidationError> Errors { get; }

    public static Result Success() => new(true, NoErrors);

    public static Result Invalid(params ValidationError[] errors) =>
        new(false, errors.Length > 0
            ? errors
            : throw new ArgumentException("A failed result needs at least one error.", nameof(errors)));

    public static Result Invalid(IReadOnlyList<ValidationError> errors) =>
        new(false, errors.Count > 0
            ? errors
            : throw new ArgumentException("A failed result needs at least one error.", nameof(errors)));

    /// <summary>Shorthand for a single error about one field.</summary>
    public static Result Invalid(string field, string message) =>
        Invalid(new ValidationError(field, message));

    /// <summary>The record the caller asked for is not there.</summary>
    public static Result NotFound(string what) =>
        Invalid(ValidationError.FormLevel, $"{what} could not be found. It may have been deleted.");

    /// <summary>Every error message, one per line. For snackbars and logs.</summary>
    public string ErrorSummary => string.Join(Environment.NewLine, Errors.Select(e => e.Message));
}

/// <summary>The outcome of a write that returns a value, usually the new id.</summary>
public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, IReadOnlyList<ValidationError> errors)
        : base(isSuccess, errors) => Value = value;

    /// <summary>Only meaningful when <see cref="Result.IsSuccess"/> is true.</summary>
    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, []);

    public static new Result<T> Invalid(params ValidationError[] errors) =>
        new(false, default, errors.Length > 0
            ? errors
            : throw new ArgumentException("A failed result needs at least one error.", nameof(errors)));

    public static new Result<T> Invalid(IReadOnlyList<ValidationError> errors) =>
        new(false, default, errors.Count > 0
            ? errors
            : throw new ArgumentException("A failed result needs at least one error.", nameof(errors)));

    public static new Result<T> Invalid(string field, string message) =>
        Invalid(new ValidationError(field, message));

    public static new Result<T> NotFound(string what) =>
        Invalid(ValidationError.FormLevel, $"{what} could not be found. It may have been deleted.");
}
