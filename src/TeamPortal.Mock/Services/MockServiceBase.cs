using FluentValidation;
using TeamPortal.Application.Common;

namespace TeamPortal.Mock.Services;

internal abstract class MockServiceBase
{
    protected MockServiceBase(MockDataStore store, MockOptions options)
    {
        Store = store;
        Options = options;
    }

    protected MockDataStore Store { get; }

    protected MockOptions Options { get; }

    protected Task ReadDelayAsync(CancellationToken ct) =>
        Options.ReadDelay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(Options.ReadDelay, ct);

    protected Task WriteDelayAsync(CancellationToken ct) =>
        Options.WriteDelay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(Options.WriteDelay, ct);

    /// <summary>
    /// The dev-only "make the next save fail" switch, as a form-level error.
    /// Returns null when the switch is off.
    /// </summary>
    protected Result? SimulatedFailure() =>
        Options.ConsumeFailureFlag()
            ? Result.Invalid(ValidationError.FormLevel, "The save could not be completed. Please try again.")
            : null;

    /// <inheritdoc cref="SimulatedFailure"/>
    protected Result<T>? SimulatedFailure<T>() =>
        Options.ConsumeFailureFlag()
            ? Result<T>.Invalid(ValidationError.FormLevel, "The save could not be completed. Please try again.")
            : null;

    /// <summary>
    /// Runs the shared validator. Returns null when the request is valid, so a
    /// service body reads as a short list of guard clauses.
    /// </summary>
    protected static async Task<Result?> ValidateAsync<T>(
        IValidator<T> validator, T request, CancellationToken ct) =>
        await validator.CheckAsync(request, ct);

    /// <inheritdoc cref="ValidateAsync{T}"/>
    protected static async Task<Result<TValue>?> ValidateAsync<T, TValue>(
        IValidator<T> validator, T request, CancellationToken ct) =>
        await validator.CheckAsync<T, TValue>(request, ct);

    /// <summary>
    /// Case-insensitive name comparison, matching how SQL Server's default
    /// collation behaves. Getting this wrong in the mock would let a duplicate
    /// through in Part A that the database rejects in Part B.
    /// </summary>
    protected static bool NameMatches(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
