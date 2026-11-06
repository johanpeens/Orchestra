using FluentValidation;
using TeamPortal.Application.Requests;

namespace TeamPortal.Application.Validators;

/// <summary>
/// Password rules, matching the Identity options configured in phase B3.
/// Kept here so the reset form can show them before Identity exists.
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 10;

    public const string Description =
        "At least 10 characters, with an uppercase letter, a lowercase letter, a number and a symbol.";
}

public sealed class SignInRequestValidator : AbstractValidator<SignInRequest>
{
    public SignInRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("That does not look like an email address.")
            .MaximumLength(FieldRules.EmailMaxLength);

        // Deliberately only "not empty". Applying the password policy to the LOGIN form
        // would tell an attacker the policy, and would lock out users whose password
        // predates a policy change.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Enter your password.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("That does not look like an email address.")
            .MaximumLength(FieldRules.EmailMaxLength);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(FieldRules.EmailMaxLength);

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("This reset link is incomplete. Request a new one.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Choose a new password.")
            .MinimumLength(PasswordRules.MinimumLength)
            .WithMessage(PasswordRules.Description)
            .Matches("[A-Z]").WithMessage(PasswordRules.Description)
            .Matches("[a-z]").WithMessage(PasswordRules.Description)
            .Matches("[0-9]").WithMessage(PasswordRules.Description)
            .Matches("[^a-zA-Z0-9]").WithMessage(PasswordRules.Description);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("The two passwords do not match.");
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Enter your name.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("That does not look like an email address.")
            .MaximumLength(FieldRules.EmailMaxLength);

        // The full policy, unlike the login form. Here it is help rather than a
        // hint to an attacker - the user is choosing the password, so telling
        // them the rules is the whole point.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Choose a password.")
            .MinimumLength(PasswordRules.MinimumLength)
            .WithMessage(PasswordRules.Description)
            .Matches("[A-Z]").WithMessage(PasswordRules.Description)
            .Matches("[a-z]").WithMessage(PasswordRules.Description)
            .Matches("[0-9]").WithMessage(PasswordRules.Description)
            .Matches("[^a-zA-Z0-9]").WithMessage(PasswordRules.Description);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("The two passwords do not match.");
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(FieldRules.EmailMaxLength);

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("This confirmation link is incomplete. Request a new one.");
    }
}
