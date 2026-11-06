using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Mock.Services;

/// <summary>
/// Holds the signed-in user for one circuit. Scoped, not singleton - two people
/// using the app at once must not share an identity.
/// </summary>
internal sealed class MockCurrentUserService : ICurrentUserService
{
    public CurrentUserDto? User { get; private set; }

    public int? UserId => User?.UserId;

    public bool IsSignedIn => User is not null;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public void SetUser(CurrentUserDto? user) => User = user;
}

internal sealed class MockAuthService : MockServiceBase, IAuthService, IDevRoleSwitcher
{
    /// <summary>Matches the Identity lockout options in Frontend/DataModel.md.</summary>
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IValidator<SignInRequest> _signInValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<ConfirmEmailRequest> _confirmValidator;
    private readonly MockCurrentUserService _currentUser;
    private readonly IEmailSender _email;
    private readonly IAppUrlProvider _urls;

    public MockAuthService(
        MockDataStore store,
        MockOptions options,
        IValidator<SignInRequest> signInValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        IValidator<RegisterRequest> registerValidator,
        IValidator<ConfirmEmailRequest> confirmValidator,
        ICurrentUserService currentUser,
        IEmailSender email,
        IAppUrlProvider urls)
        : base(store, options)
    {
        _signInValidator = signInValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _registerValidator = registerValidator;
        _confirmValidator = confirmValidator;
        _currentUser = (MockCurrentUserService)currentUser;
        _email = email;
        _urls = urls;
    }

    public async Task<SignInResultDto> SignInAsync(
        SignInRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (!(await _signInValidator.ValidateAsync(request, ct)).IsValid)
        {
            return SignInResultDto.Failed(SignInFailureReason.InvalidCredentials);
        }

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => NameMatches(u.Email, request.Email));

            // No such account. Same answer as a wrong password, and the same
            // amount of work, so neither the message nor the timing reveals
            // whether the address exists.
            if (user is null)
            {
                return SignInResultDto.Failed(SignInFailureReason.InvalidCredentials);
            }

            if (user.LockoutEnd is { } until && until > DateTimeOffset.UtcNow)
            {
                return SignInResultDto.Failed(SignInFailureReason.LockedOut, until);
            }

            if (!string.Equals(user.Password, request.Password, StringComparison.Ordinal))
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTimeOffset.UtcNow.Add(LockoutDuration);
                    return SignInResultDto.Failed(SignInFailureReason.LockedOut, user.LockoutEnd);
                }

                return SignInResultDto.Failed(SignInFailureReason.InvalidCredentials);
            }

            // Correct password, but the account is switched off. Deliberately
            // reported as InvalidCredentials so a disabled account is not
            // distinguishable from a wrong one.
            if (!user.IsActive)
            {
                return SignInResultDto.Failed(SignInFailureReason.Inactive);
            }

            // Gate one: registered but never confirmed. Identity refuses this too
            // - see IdentityAuthService - and the contract runs against both.
            if (!user.EmailConfirmed)
            {
                return SignInResultDto.Failed(SignInFailureReason.EmailNotConfirmed);
            }

            // Gate two: confirmed, but nobody has decided they belong here yet.
            if (user.ApprovalStatus != ApprovalStatus.Approved)
            {
                return SignInResultDto.Failed(
                    user.ApprovalStatus == ApprovalStatus.Rejected
                        ? SignInFailureReason.ApprovalRejected
                        : SignInFailureReason.AwaitingApproval);
            }

            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;

            CurrentUserDto dto = Project(user);
            _currentUser.SetUser(dto);

            Store.WriteActivity(
                ActivityAction.LoggedIn, "AppUser", user.Id, $"{user.FullName} signed in", user.Id);

            return SignInResultDto.Success(dto);
        }
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        lock (Store.Gate)
        {
            if (_currentUser.User is { } user)
            {
                Store.WriteActivity(
                    ActivityAction.LoggedOut, "AppUser", user.UserId,
                    $"{user.FullName} signed out", user.UserId);
            }

            _currentUser.SetUser(null);
        }
    }

    public async Task<Result> RequestPasswordResetAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await _forgotValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        string email = request.Email.Trim();
        string? fullName = null;
        string token = Guid.NewGuid().ToString("N");

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => NameMatches(u.Email, email));

            if (user is not null)
            {
                fullName = user.FullName;
            }
        }

        // The link goes to the address on file, never back to the caller, and
        // only when there is an account behind it.
        if (fullName is not null)
        {
            string url = _urls.Absolute("reset-password", ("email", email), ("token", token));

            await _email.SendAsync(EmailBodies.ResetPassword(email, fullName, url), ct);
        }

        // Always succeeds, whether or not the address is known. Reporting
        // "no such account" here would hand an attacker a user enumeration tool.
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await _resetValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => NameMatches(u.Email, request.Email));

            if (user is null)
            {
                return Result.Invalid(
                    ValidationError.FormLevel, "This reset link is no longer valid. Request a new one.");
            }

            user.Password = request.NewPassword;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            Store.WriteActivity(
                ActivityAction.Updated, "AppUser", user.Id, $"{user.FullName} reset their password", user.Id);

            return Result.Success();
        }
    }

    public async Task<Result> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await _registerValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        string email = request.Email.Trim();
        string token;
        string fullName;
        bool isNewAccount;

        lock (Store.Gate)
        {
            MockUser? existing = Store.Users.FirstOrDefault(u => NameMatches(u.Email, email));

            if (existing is not null)
            {
                // Reported as success. The difference goes in the email, not in
                // the response, so the form cannot be used to discover which
                // addresses are already registered.
                token = Guid.NewGuid().ToString("N");
                fullName = existing.FullName;
                isNewAccount = false;
            }
            else
            {
                token = Guid.NewGuid().ToString("N");
                fullName = request.FullName.Trim();
                isNewAccount = true;

                int id = Store.NextUserId();

                Store.Users.Add(new MockUser
                {
                    Id = id,
                    Email = email,
                    Password = request.Password,
                    FullName = fullName,
                    EmailConfirmed = false,
                    EmailConfirmationToken = token,

                    // Both gates start shut. Confirming the email opens the
                    // first; an administrator opens the second. No role at all
                    // until then - approval is what grants one.
                    ApprovalStatus = ApprovalStatus.Pending,
                    RegisteredAt = DateTime.UtcNow,
                    Roles = []
                });

                Store.WriteActivity(
                    ActivityAction.Created, "AppUser", id,
                    $"{fullName} registered and is awaiting confirmation", id);
            }
        }

        string url = _urls.Absolute(
            isNewAccount ? "confirm-email" : "reset-password",
            ("email", email),
            ("token", token));

        EmailMessage message = isNewAccount
            ? EmailBodies.ConfirmEmail(email, fullName, url)
            : EmailBodies.ResetPassword(email, fullName, url);

        Result sent = await _email.SendAsync(message, ct);

        return sent.IsSuccess ? Result.Success() : sent;
    }

    public async Task<Result> ConfirmEmailAsync(
        ConfirmEmailRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await _confirmValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => NameMatches(u.Email, request.Email));

            if (user is null)
            {
                return InvalidConfirmationToken();
            }

            // Already done. People click links twice.
            if (user.EmailConfirmed)
            {
                return Result.Success();
            }

            if (!string.Equals(user.EmailConfirmationToken, request.Token, StringComparison.Ordinal))
            {
                return InvalidConfirmationToken();
            }

            user.EmailConfirmed = true;

            // Single use, like Identity's - which invalidates the token by
            // changing the security stamp rather than by storing one.
            user.EmailConfirmationToken = null;

            Store.WriteActivity(
                ActivityAction.Updated, "AppUser", user.Id,
                $"{user.FullName} confirmed their email address", user.Id);

            return Result.Success();
        }
    }

    private static Result InvalidConfirmationToken() =>
        Result.Invalid(
            ValidationError.FormLevel,
            "This confirmation link is no longer valid. Register again to get a new one.");

    /// <inheritdoc />
    public CurrentUserDto? SwitchToRole(string role)
    {
        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(
                u => u.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));

            if (user is null)
            {
                return null;
            }

            CurrentUserDto dto = Project(user);
            _currentUser.SetUser(dto);
            return dto;
        }
    }

    private static CurrentUserDto Project(MockUser user) =>
        new(user.Id, user.FullName, user.Email, user.PhotoUrl, [.. user.Roles]);
}
