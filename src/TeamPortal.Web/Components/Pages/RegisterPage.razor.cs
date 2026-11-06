using FluentValidation;
using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Pages;

public partial class RegisterPage : ComponentBase
{
    private readonly RegisterModel _model = new();

    private bool _showPassword;
    private bool _submitting;
    private bool _sent;
    private string _submittedEmail = string.Empty;
    private string? _formError;
    private string? _nameError;
    private string? _emailError;
    private string? _passwordError;
    private string? _confirmError;

    [Inject]
    private IAuthService Auth { get; set; } = null!;

    [Inject]
    private IValidator<RegisterRequest> Validator { get; set; } = null!;

    private bool CanSubmit =>
        !_submitting &&
        !string.IsNullOrWhiteSpace(_model.FullName) &&
        !string.IsNullOrWhiteSpace(_model.Email) &&
        !string.IsNullOrWhiteSpace(_model.Password) &&
        !string.IsNullOrWhiteSpace(_model.ConfirmPassword);

    private void TogglePassword() => _showPassword = !_showPassword;

    private void ClearErrors()
    {
        _formError = null;
        _nameError = null;
        _emailError = null;
        _passwordError = null;
        _confirmError = null;
    }

    private async Task RegisterAsync()
    {
        if (_submitting)
        {
            return;
        }

        ClearErrors();
        _submitting = true;

        try
        {
            var request = new RegisterRequest(
                _model.FullName.Trim(),
                _model.Email.Trim(),
                _model.Password,
                _model.ConfirmPassword);

            // Same validator the service uses, so the messages match whichever
            // implementation is behind the interface.
            if (await Validator.CheckAsync(request) is { } invalid)
            {
                ApplyErrors(invalid.Errors);
                return;
            }

            Result result = await Auth.RegisterAsync(request);

            if (result.IsFailure)
            {
                ApplyErrors(result.Errors);

                // Both password boxes are cleared on any failure. Leaving a
                // rejected password on screen invites the user to tweak it by one
                // character rather than read the rule.
                _model.Password = string.Empty;
                _model.ConfirmPassword = string.Empty;
                return;
            }

            // Deliberately the same screen whether the address was new or already
            // registered. Telling them apart here would undo the whole point of
            // the service reporting both as success.
            _submittedEmail = request.Email;
            _sent = true;
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
                case nameof(RegisterRequest.FullName):
                    _nameError ??= error.Message;
                    break;
                case nameof(RegisterRequest.Email):
                    _emailError ??= error.Message;
                    break;
                case nameof(RegisterRequest.Password):
                    _passwordError ??= error.Message;
                    break;
                case nameof(RegisterRequest.ConfirmPassword):
                    _confirmError ??= error.Message;
                    break;
                default:
                    _formError ??= error.Message;
                    break;
            }
        }
    }

    /// <summary>The bound form. A plain class so two-way binding works.</summary>
    private sealed class RegisterModel
    {
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
