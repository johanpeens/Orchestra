using FluentValidation;
using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Pages;

public partial class ResetPasswordPage : ComponentBase
{
    private readonly ResetModel _model = new();

    private bool _showPassword;
    private bool _submitting;
    private bool _done;
    private string? _formError;
    private string? _passwordError;
    private string? _confirmError;

    [Inject]
    private IAuthService Auth { get; set; } = null!;

    [Inject]
    private IValidator<ResetPasswordRequest> Validator { get; set; } = null!;

    // Public for the same reason as ConfirmEmailPage - see the note there.
    [SupplyParameterFromQuery(Name = "email")]
    public string? Email { get; set; }

    [SupplyParameterFromQuery(Name = "token")]
    public string? Token { get; set; }

    private bool CanSubmit =>
        !_submitting &&
        !string.IsNullOrWhiteSpace(_model.NewPassword) &&
        !string.IsNullOrWhiteSpace(_model.ConfirmPassword);

    private void TogglePassword() => _showPassword = !_showPassword;

    private void ClearErrors()
    {
        _formError = null;
        _passwordError = null;
        _confirmError = null;
    }

    private async Task SubmitAsync()
    {
        if (_submitting)
        {
            return;
        }

        ClearErrors();
        _submitting = true;

        try
        {
            var request = new ResetPasswordRequest(
                Email ?? string.Empty,
                Token ?? string.Empty,
                _model.NewPassword,
                _model.ConfirmPassword);

            if (await Validator.CheckAsync(request) is { } invalid)
            {
                ApplyErrors(invalid.Errors);
                return;
            }

            Result result = await Auth.ResetPasswordAsync(request);

            if (result.IsFailure)
            {
                ApplyErrors(result.Errors);

                // Both boxes cleared. A rejected password left on screen invites
                // tweaking it by a character rather than reading the rule.
                _model.NewPassword = string.Empty;
                _model.ConfirmPassword = string.Empty;
                return;
            }

            _done = true;
        }
        finally
        {
            _submitting = false;
        }
    }

    private void ApplyErrors(IReadOnlyList<ValidationError> errors)
    {
        foreach (ValidationError error in errors)
        {
            switch (error.Field)
            {
                case nameof(ResetPasswordRequest.NewPassword):
                    _passwordError ??= error.Message;
                    break;
                case nameof(ResetPasswordRequest.ConfirmPassword):
                    _confirmError ??= error.Message;
                    break;
                default:
                    // Includes a bad or missing token, which belongs to the form
                    // rather than to any box the user can edit.
                    _formError ??= error.Message;
                    break;
            }
        }
    }

    private sealed class ResetModel
    {
        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
