using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Pages;

public partial class ConfirmEmailPage : ComponentBase
{
    private bool _checking = true;
    private bool _confirmed;
    private string _error = "Request a new link and try again.";

    [Inject]
    private IAuthService Auth { get; set; } = null!;

    // Public because these are component parameters - a test sets them the way
    // the router would, and the router is not in the loop in a bUnit test.
    [SupplyParameterFromQuery(Name = "email")]
    public string? Email { get; set; }

    [SupplyParameterFromQuery(Name = "token")]
    public string? Token { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // A missing parameter is a truncated link - some mail clients wrap long
        // URLs and lose the tail. Same message as a bad token, because from the
        // reader's side it is the same problem.
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token))
        {
            _error = "That link looks incomplete. Copy the whole address from the email, " +
                     "or register again to get a new one.";
            _checking = false;
            return;
        }

        try
        {
            Result result = await Auth.ConfirmEmailAsync(new ConfirmEmailRequest(Email, Token));

            _confirmed = result.IsSuccess;

            if (result.IsFailure)
            {
                _error = result.ErrorSummary;
            }
        }
        finally
        {
            _checking = false;
        }
    }
}
