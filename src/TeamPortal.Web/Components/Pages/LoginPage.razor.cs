using FluentValidation;
using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Pages;

public partial class LoginPage : ComponentBase
{
    private readonly LoginModel _model = new();

    private bool _showPassword;
    private bool _submitting;
    private string? _formError;
    private string? _emailError;
    private string? _passwordError;

    [Inject]
    private IAuthService Auth { get; set; } = null!;

    [Inject]
    private IValidator<SignInRequest> Validator { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>
    /// Where to land after a successful sign-in. Set by the redirect-to-login
    /// flow so a deep link survives the detour through this page.
    /// </summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrl { get; set; }

    /// <summary>
    /// The button stays disabled until both boxes have something in them, and
    /// while a sign-in is in flight so a double-click cannot fire twice.
    /// </summary>
    private bool CanSubmit =>
        !_submitting &&
        !string.IsNullOrWhiteSpace(_model.Email) &&
        !string.IsNullOrWhiteSpace(_model.Password);

    private void TogglePassword() => _showPassword = !_showPassword;

    /// <summary>Clears stale errors as soon as the user edits either field.</summary>
    private void ClearErrors()
    {
        _formError = null;
        _emailError = null;
        _passwordError = null;
    }

    private async Task SignInAsync()
    {
        if (_submitting)
        {
            return;
        }

        ClearErrors();
        _submitting = true;

        try
        {
            var request = new SignInRequest(
                _model.Email.Trim(), _model.Password, _model.RememberMe);

            // Shape check first - the same validator the service uses, so the
            // messages match whatever Part B's implementation produces.
            if (await Validator.CheckAsync(request) is { } invalid)
            {
                ApplyErrors(invalid.Errors);
                return;
            }

            SignInResultDto result = await Auth.SignInAsync(request);

            if (!result.Succeeded)
            {
                // One message for both wrong-email and wrong-password. The
                // password box is deliberately cleared and the email kept, so a
                // typo does not cost the user both fields.
                _formError = result.Message;
                _model.Password = string.Empty;
                return;
            }

            // forceLoad, and via the endpoint. A circuit cannot write the
            // authentication cookie - see SignInResultDto.Ticket - so the browser
            // has to make one more real request for the cookie to be set. Without
            // this the user looks signed in until the first refresh, and then is
            // bounced back to this page.
            string destination = Uri.EscapeDataString(SafeReturnUrl());

            Navigation.NavigateTo(
                result.Ticket is { Length: > 0 } ticket
                    ? $"/account/complete-sign-in?ticket={Uri.EscapeDataString(ticket)}&returnUrl={destination}"
                    : SafeReturnUrl(),
                forceLoad: true);
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
            if (error.Field == nameof(SignInRequest.Email))
            {
                _emailError ??= error.Message;
            }
            else if (error.Field == nameof(SignInRequest.Password))
            {
                _passwordError ??= error.Message;
            }
            else
            {
                _formError ??= error.Message;
            }
        }
    }

    /// <summary>
    /// Only ever returns a path on this site. An attacker who can put
    /// ?returnUrl=https://evil.example on a link must not be able to bounce a
    /// freshly signed-in user off-site.
    /// </summary>
    private string SafeReturnUrl()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl))
        {
            return "/";
        }

        if (!Uri.TryCreate(ReturnUrl, UriKind.Relative, out _))
        {
            return "/";
        }

        // Protocol-relative URLs ("//evil.example") parse as relative but leave
        // the site, so reject anything that does not start with a single slash.
        return ReturnUrl.StartsWith('/') && !ReturnUrl.StartsWith("//", StringComparison.Ordinal)
            ? ReturnUrl
            : "/";
    }

    /// <summary>The bound form. A plain class so two-way binding works.</summary>
    private sealed class LoginModel
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
