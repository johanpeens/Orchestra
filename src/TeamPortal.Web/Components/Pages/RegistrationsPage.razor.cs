using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;

namespace TeamPortal.Web.Components.Pages;

public partial class RegistrationsPage : ComponentBase
{
    /// <summary>
    /// Waiting first. The queue is what this screen is for; the other tabs are
    /// for looking something up.
    /// </summary>
    private static readonly ApprovalFilter[] Tabs =
    [
        ApprovalFilter.AwaitingDecision,
        ApprovalFilter.Approved,
        ApprovalFilter.Rejected,
        ApprovalFilter.All
    ];

    private IReadOnlyList<UserAccountDto> _accounts = [];
    private bool _loading = true;
    private string? _error;
    private int _activeTab;
    private int? _busyUserId;

    [Inject]
    private IUserApprovalService Approvals { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private ApprovalFilter CurrentFilter => Tabs[Math.Clamp(_activeTab, 0, Tabs.Length - 1)];

    private string EmptyMessage => CurrentFilter switch
    {
        ApprovalFilter.AwaitingDecision => "Nobody is waiting for a decision right now.",
        ApprovalFilter.Approved => "No accounts have been approved yet.",
        ApprovalFilter.Rejected => "Nothing has been turned down.",
        _ => "Nobody has registered yet."
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// The tab index is bound, so a change lands here rather than in a handler.
    /// Reloading on every render would loop; comparing against what is loaded is
    /// what stops that.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFilter != CurrentFilter)
        {
            await ReloadAsync();
        }
    }

    private ApprovalFilter? _loadedFilter;

    private async Task ReloadAsync()
    {
        _loading = true;
        _error = null;

        try
        {
            _accounts = await Approvals.GetAllAsync(CurrentFilter);
            _loadedFilter = CurrentFilter;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ApproveAsync(UserAccountDto account)
    {
        // Approving grants Viewer. Changing it to something else is a second,
        // deliberate action on the row - so nobody gives out an editor role by
        // clicking the obvious button.
        await RunAsync(account, () => Approvals.ApproveAsync(account.UserId, AppRoles.Viewer),
            $"{account.FullName} can sign in now. We have emailed them.");
    }

    private async Task RejectAsync(UserAccountDto account)
    {
        await RunAsync(account, () => Approvals.RejectAsync(account.UserId),
            $"{account.FullName} has been turned down.");
    }

    private async Task ChangeRoleAsync(UserAccountDto account, string role)
    {
        if (string.Equals(role, account.PrimaryRole, StringComparison.Ordinal))
        {
            return;
        }

        await RunAsync(account, () => Approvals.ChangeRoleAsync(account.UserId, role),
            $"{account.FullName} is now {role}.");
    }

    /// <summary>
    /// One place for the parts every action shares: the row is disabled while it
    /// runs, the grid is reloaded afterwards, and a refusal is shown rather than
    /// swallowed. The service can say no even to an administrator - rejecting
    /// your own account, for one - and that message is worth reading.
    /// </summary>
    private async Task RunAsync(UserAccountDto account, Func<Task<Result>> action, string success)
    {
        if (_busyUserId is not null)
        {
            return;
        }

        _busyUserId = account.UserId;

        try
        {
            Result result = await action();

            if (result.IsFailure)
            {
                Snackbar.Add(result.ErrorSummary, Severity.Error);
                return;
            }

            Snackbar.Add(success, Severity.Success);
            await ReloadAsync();
        }
        finally
        {
            _busyUserId = null;
        }
    }

    private static string TabLabel(ApprovalFilter filter) => filter switch
    {
        ApprovalFilter.AwaitingDecision => "Waiting",
        ApprovalFilter.Approved => "Approved",
        ApprovalFilter.Rejected => "Rejected",
        _ => "All"
    };

    private static string StatusLabel(UserAccountDto account) => account switch
    {
        { IsAwaitingConfirmation: true } => "Email not confirmed",
        { IsAwaitingDecision: true } => "Waiting for you",
        { ApprovalStatus: ApprovalStatus.Approved } => "Approved",
        { ApprovalStatus: ApprovalStatus.Rejected } => "Rejected",
        _ => "Pending"
    };

    private static Color StatusColour(UserAccountDto account) => account switch
    {
        { IsAwaitingConfirmation: true } => Color.Default,
        { IsAwaitingDecision: true } => Color.Warning,
        { ApprovalStatus: ApprovalStatus.Approved } => Color.Success,
        { ApprovalStatus: ApprovalStatus.Rejected } => Color.Error,
        _ => Color.Default
    };

    /// <summary>"3 days ago" rather than a timestamp - the age is what matters.</summary>
    private static string Ago(DateTime? when)
    {
        if (when is not DateTime value)
        {
            return "-";
        }

        TimeSpan elapsed = DateTime.UtcNow - value;

        return elapsed switch
        {
            { TotalMinutes: < 2 } => "just now",
            { TotalHours: < 1 } => $"{(int)elapsed.TotalMinutes} min ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours} h ago",
            { TotalDays: < 30 } => $"{(int)elapsed.TotalDays} d ago",
            _ => value.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
