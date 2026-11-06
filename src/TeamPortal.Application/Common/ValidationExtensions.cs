using FluentValidation;
using FluentValidation.Results;

namespace TeamPortal.Application.Common;

public static class ValidationExtensions
{
    /// <summary>
    /// One <see cref="ValidationError"/> per failure. Property paths are flattened, so
    /// <c>OwnerIds[2]</c> becomes <c>OwnerIds</c> and the message lands on the right
    /// field in the UI rather than on nothing.
    /// </summary>
    public static IReadOnlyList<ValidationError> ToErrors(this ValidationResult result) =>
        [.. result.Errors.Select(f => new ValidationError(FlattenPath(f.PropertyName), f.ErrorMessage))];

    /// <summary>
    /// Runs the validator and returns null when everything passes, so a service reads:
    /// <c>if (await validator.CheckAsync(request, ct) is { } failure) return failure;</c>
    /// </summary>
    public static async Task<Result?> CheckAsync<T>(
        this IValidator<T> validator, T request, CancellationToken ct = default)
    {
        ValidationResult result = await validator.ValidateAsync(request, ct);
        return result.IsValid ? null : Result.Invalid(result.ToErrors());
    }

    /// <inheritdoc cref="CheckAsync{T}(IValidator{T}, T, CancellationToken)"/>
    public static async Task<Result<TValue>?> CheckAsync<T, TValue>(
        this IValidator<T> validator, T request, CancellationToken ct = default)
    {
        ValidationResult result = await validator.ValidateAsync(request, ct);
        return result.IsValid ? null : Result<TValue>.Invalid(result.ToErrors());
    }

    private static string FlattenPath(string propertyName)
    {
        int bracket = propertyName.IndexOf('[', StringComparison.Ordinal);
        return bracket >= 0 ? propertyName[..bracket] : propertyName;
    }
}
