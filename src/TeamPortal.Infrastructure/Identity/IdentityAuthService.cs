using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Domain.Enums;
using TeamPortal.Domain.Identity;
using AppEnums = TeamPortal.Application.Enums;

namespace TeamPortal.Infrastructure.Identity;

internal sealed class IdentityAuthService : IAuthService
{
    /// <summary>Matches the lockout options in Frontend/DataModel.md.</summary>
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly ISignInTicketStore _tickets;
    private readonly CurrentUserState _state;
    private readonly IActivityLogService _activityLog;
    private readonly IEmailSender _email;
    private readonly IAppUrlProvider _urls;
    private readonly IValidator<SignInRequest> _signInValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<ConfirmEmailRequest> _confirmValidator;

    public IdentityAuthService(
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        ISignInTicketStore tickets,
        CurrentUserState state,
        IActivityLogService activityLog,
        IEmailSender email,
        IAppUrlProvider urls,
        IValidator<SignInRequest> signInValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        IValidator<RegisterRequest> registerValidator,
        IValidator<ConfirmEmailRequest> confirmValidator)
    {
        _users = users;
        _signIn = signIn;
        _tickets = tickets;
        _state = state;
        _activityLog = activityLog;
        _email = email;
        _urls = urls;
        _signInValidator = signInValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _registerValidator = registerValidator;
        _confirmValidator = confirmValidator;
    }

    public async Task<SignInResultDto> SignInAsync(
        SignInRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!(await _signInValidator.ValidateAsync(request, ct)).IsValid)
        {
            return SignInResultDto.Failed(SignInFailureReason.InvalidCredentials);
        }

        AppUser? user = await _users.FindByEmailAsync(request.Email);

        // No such account. Same answer as a wrong password, so neither the
        // message nor the outcome reveals whether the address exists.
        if (user is null)
        {
            return SignInResultDto.Failed(SignInFailureReason.InvalidCredentials);
        }

        SignInResult result = await _signIn.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return SignInResultDto.Failed(
                SignInFailureReason.LockedOut, await _users.GetLockoutEndDateAsync(user));
        }

        if (!result.Succeeded)
        {
            return SignInResultDto.Failed(SignInFailureReason.InvalidCredentials);
        }

        // Correct password, but the account is switched off. Reported without a
        // distinguishing message, for the same reason as above.
        if (!user.IsActive)
        {
            return SignInResultDto.Failed(SignInFailureReason.Inactive);
        }

        // Gate one: registered but never confirmed. Without this the confirmation
        // email is decoration - anyone could register any address and sign in
        // regardless.
        if (!await _users.IsEmailConfirmedAsync(user))
        {
            return SignInResultDto.Failed(SignInFailureReason.EmailNotConfirmed);
        }

        // Gate two: confirmed, but nobody has decided they belong here yet.
        // Confirming an address only proves somebody can read that mailbox.
        if (user.ApprovalStatus != ApprovalStatus.Approved)
        {
            return SignInResultDto.Failed(
                user.ApprovalStatus == ApprovalStatus.Rejected
                    ? SignInFailureReason.ApprovalRejected
                    : SignInFailureReason.AwaitingApproval);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        // Deliberately NOT _signIn.SignInAsync here. That appends a Set-Cookie
        // header, and this runs inside an interactive Blazor circuit where the
        // response finished long ago - it throws "Headers are read-only, response
        // has already started". The cookie is written by the endpoint that
        // redeems the ticket below, inside a real request.
        string ticket = _tickets.Issue(user.Id, request.RememberMe);

        // The circuit's own view of who is signed in. Set here so the current
        // page updates immediately, before the redirect completes.
        CurrentUserDto dto = await ProjectAsync(user);
        _state.SetUser(dto);

        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.LoggedIn, "AppUser", user.Id, $"{user.FullName} signed in", ct);

        return SignInResultDto.Success(dto, ticket);
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        if (_state.User is { } user)
        {
            await _activityLog.WriteAsync(
                AppEnums.ActivityAction.LoggedOut, "AppUser", user.UserId,
                $"{user.FullName} signed out", ct);
        }

        // Not _signIn.SignOutAsync() either - that deletes the cookie by writing a
        // Set-Cookie header, and fails from a circuit for the same reason signing
        // in does. The cookie is cleared by /account/sign-out, which the account
        // menu navigates to. Clearing the circuit's own view is what this does.
        _state.SetUser(null);

        await Task.CompletedTask;
    }

    public async Task<Result> RequestPasswordResetAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _forgotValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        AppUser? user = await _users.FindByEmailAsync(request.Email);

        if (user is not null)
        {
            // The token goes to the address on file, never back to the caller.
            string token = await _users.GeneratePasswordResetTokenAsync(user);

            string url = _urls.Absolute(
                "reset-password",
                ("email", user.Email ?? string.Empty),
                ("token", token));

            await _email.SendAsync(
                EmailBodies.ResetPassword(user.Email ?? string.Empty, user.FullName, url), ct);

            await _activityLog.WriteAsync(
                AppEnums.ActivityAction.Updated, "AppUser", user.Id,
                $"A password reset was requested for {user.Email}", ct);
        }

        // Always succeeds, whether or not the address is known. Reporting
        // "no such account" here would hand an attacker a user enumeration tool.
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _resetValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        AppUser? user = await _users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            // Same message a bad token gets - see the note above.
            return InvalidToken();
        }

        IdentityResult reset = await _users.ResetPasswordAsync(
            user, request.Token, request.NewPassword);

        if (!reset.Succeeded)
        {
            // Identity's own messages name the exact rule that failed, which is
            // useful for a password and unhelpful for a token. Password rules are
            // already checked by the validator, so anything reaching here is the
            // token.
            return InvalidToken();
        }

        // A reset is also a way out of a lockout.
        await _users.ResetAccessFailedCountAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);

        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.Updated, "AppUser", user.Id,
            $"{user.FullName} reset their password", ct);

        return Result.Success();
    }

    public async Task<Result> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _registerValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        string email = request.Email.Trim();
        AppUser? existing = await _users.FindByEmailAsync(email);

        if (existing is not null)
        {
            // Reported as success. The difference goes in the email - the owner of
            // an existing account gets a password-reset link rather than a
            // confirmation - so the form cannot be used to discover which
            // addresses are already registered.
            string resetToken = await _users.GeneratePasswordResetTokenAsync(existing);

            string resetUrl = _urls.Absolute(
                "reset-password", ("email", email), ("token", resetToken));

            await _email.SendAsync(
                EmailBodies.ResetPassword(email, existing.FullName, resetUrl), ct);

            return Result.Success();
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            EmailConfirmed = false,
            IsActive = true,

            // Both gates start shut. Confirming the email opens the first; an
            // administrator opens the second.
            ApprovalStatus = ApprovalStatus.Pending,
            RegisteredAt = DateTime.UtcNow
        };

        IdentityResult created = await _users.CreateAsync(user, request.Password);

        if (!created.Succeeded)
        {
            // Identity names the rule that failed, which is worth showing for a
            // password. Anything else here is a duplicate-account race, and that
            // is reported as success for the same reason as the branch above.
            IReadOnlyList<ValidationError> errors =
            [
                .. created.Errors
                    .Where(e => e.Code.Contains("Password", StringComparison.Ordinal))
                    .Select(e => new ValidationError(nameof(RegisterRequest.Password), e.Description))
            ];

            return errors.Count > 0 ? Result.Invalid(errors) : Result.Success();
        }

        // No role at all yet. Approving the account is what grants one, and the
        // admin picks which - see IUserApprovalService.ApproveAsync.
        string token = await _users.GenerateEmailConfirmationTokenAsync(user);

        string confirmUrl = _urls.Absolute(
            "confirm-email", ("email", email), ("token", token));

        Result sent = await _email.SendAsync(
            EmailBodies.ConfirmEmail(email, user.FullName, confirmUrl), ct);

        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.Created, "AppUser", user.Id,
            $"{user.FullName} registered and is awaiting confirmation", ct);

        // The account exists either way. If the email did not go out, say so -
        // succeeding silently leaves someone waiting for a message that is never
        // coming.
        return sent.IsSuccess ? Result.Success() : sent;
    }

    public async Task<Result> ConfirmEmailAsync(
        ConfirmEmailRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _confirmValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        AppUser? user = await _users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidConfirmationToken();
        }

        // Already done. People click links twice, and a forwarded email gets
        // opened by the recipient after the sender has already used it.
        if (await _users.IsEmailConfirmedAsync(user))
        {
            return Result.Success();
        }

        IdentityResult confirmed = await _users.ConfirmEmailAsync(user, request.Token);

        if (!confirmed.Succeeded)
        {
            return InvalidConfirmationToken();
        }

        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.Updated, "AppUser", user.Id,
            $"{user.FullName} confirmed their email address", ct);

        return Result.Success();
    }

    private static Result InvalidConfirmationToken() =>
        Result.Invalid(
            ValidationError.FormLevel,
            "This confirmation link is no longer valid. Register again to get a new one.");

    private static Result InvalidToken() =>
        Result.Invalid(
            ValidationError.FormLevel, "This reset link is no longer valid. Request a new one.");

    private async Task<CurrentUserDto> ProjectAsync(AppUser user)
    {
        IList<string> roles = await _users.GetRolesAsync(user);

        return new CurrentUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.PhotoUrl, [.. roles]);
    }
}

/// <summary>
/// Who is signed in, for the life of one scope - and in Blazor Server one scope is
/// one circuit.
///
/// Identity's own answer lives in the authentication cookie, which a circuit
/// cannot read after the initial render, and which does not exist at all in a
/// test. This holds the same answer somewhere both can reach.
/// </summary>
public sealed class CurrentUserState
{
    public CurrentUserDto? User { get; private set; }

    public void SetUser(CurrentUserDto? user) => User = user;
}

/// <summary>
/// Reads the signed-in user for the audit columns and the app bar. Prefers what
/// this circuit has been told, and falls back to the claims on the request - which
/// is what a page reached by URL with an existing cookie has.
/// </summary>
internal sealed class IdentityCurrentUserService : ICurrentUserService
{
    private readonly CurrentUserState _state;
    private readonly IClaimsPrincipalAccessor _principal;

    public IdentityCurrentUserService(CurrentUserState state, IClaimsPrincipalAccessor principal)
    {
        _state = state;
        _principal = principal;
    }

    public CurrentUserDto? User => _state.User ?? _principal.Current;

    public int? UserId => User?.UserId;

    public bool IsSignedIn => User is not null;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}

/// <summary>
/// The signed-in user as the framework sees it. Implemented over HttpContext in
/// the web project, which is the only place that knows what an HttpContext is.
/// </summary>
public interface IClaimsPrincipalAccessor
{
    CurrentUserDto? Current { get; }
}

/// <summary>Used wherever there is no request to read - the seeder, tests.</summary>
public sealed class NoClaimsPrincipalAccessor : IClaimsPrincipalAccessor
{
    public CurrentUserDto? Current => null;
}
