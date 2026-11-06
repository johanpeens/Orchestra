using FluentValidation;
using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Pages;

public partial class ForgotPasswordPage : ComponentBase
{
    private readonly ForgotModel _model = new();

    private bool _submitting;
    private bool _sent;
    private string _submittedEmail = string.Empty;
    private string? _formError;
    private string? _emailError;

    [Inject]
    private IAuthService Auth { get; set; } = null!;

    [Inject]
    private IValidator<ForgotPasswordRequest> Validator { get; set; } = null!;

    private bool CanSubmit => !_submitting && !string.IsNullOrWhiteSpace(_model.Email);

    private void ClearErrors()
    {
        _formError = null;
        _emailError = null;
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
            var request = new ForgotPasswordRequest(_model.Email.Trim());

            if (await Validator.CheckAsync(request) is { } invalid)
            {
                foreach (ValidationError error in invalid.Errors)
                {
                    if (error.Field == nameof(ForgotPasswordRequest.Email))
                    {
                        _emailError ??= error.Message;
                    }
                    else
                    {
                        _formError ??= error.Message;
                    }
                }

                return;
            }

            Result result = await Auth.RequestPasswordResetAsync(request);

            if (result.IsFailure)
            {
                _formError = result.ErrorSummary;
                return;
            }

            // The same screen whether or not the address has an account. Anything
            // else here turns this form into a way to test which addresses do.
            _submittedEmail = request.Email;
            _sent = true;
        }
        finally
        {
            _submitting = false;
        }
    }

    private sealed class ForgotModel
    {
        public string Email { get; set; } = string.Empty;
    }
}
