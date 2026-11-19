using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// Home page of the three roles that approve things.
/// Chairperson: Faculty accounts + schedule change requests of the department.
/// Dean: Chairperson accounts + classroom change requests for the school's rooms.
/// IMS Personnel: READS Scholar accounts + approved changes waiting to be applied to the master schedule.
/// All rules stay in ApprovalService and ChangeRequestService; this page only shows their lists and calls them.
/// </summary>
public partial class ApprovalsHomeViewModel : PageViewModelBase
{
    private readonly ApprovalService _approvals;
    private readonly ChangeRequestService _changes;
    private readonly SessionService _session;

    public ApprovalsHomeViewModel(ApprovalService approvals, ChangeRequestService changes, SessionService session)
    {
        _approvals = approvals;
        _changes = changes;
        _session = session;
    }

    public ObservableCollection<PendingApproval> Accounts { get; } = new();
    public ObservableCollection<ListRow> Requests { get; } = new();

    [ObservableProperty] private string _welcome = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _accountsHeading = "Accounts waiting for approval";
    [ObservableProperty] private string _accountsExplanation = "";
    [ObservableProperty] private bool _accountsEmpty;
    [ObservableProperty] private string _requestsHeading = "Requests";
    [ObservableProperty] private string _requestsExplanation = "";
    [ObservableProperty] private string _requestsEmptyText = "";
    [ObservableProperty] private bool _requestsEmpty;
    [ObservableProperty] private string _pendingText = "";
    [ObservableProperty] private bool _hasPendingText;

    /// <summary>Chairperson and Dean approve or decline requests; IMS Personnel apply the approved ones.</summary>
    [ObservableProperty] private bool _isApprover;
    [ObservableProperty] private bool _isIms;

    /// <summary>Called when the page appears: clears old messages and reloads.</summary>
    public async Task LoadAsync()
    {
        ClearMessages();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var role = _session.Role;
        var me = _session.CurrentUser;
        IsIms = role == UserRole.ImsPersonnel;
        IsApprover = role is UserRole.Chairperson or UserRole.Dean;
        Welcome = me is null ? "Welcome" : $"Welcome, {me.FullName}";

        // ---- Accounts waiting for approval
        var who = role is null ? "Accounts" : ApprovalService.WhatApproved(role.Value);
        AccountsExplanation = role switch
        {
            UserRole.Dean => "Chairpersons of your school who confirmed their email. They cannot sign in until you approve them.",
            UserRole.ImsPersonnel => "READS Scholars who confirmed their email. They cannot sign in until you approve them.",
            _ => "Faculty of your department who confirmed their email. They cannot sign in until you approve them."
        };
        Accounts.Clear();
        foreach (var p in await _approvals.GetPendingAsync()) Accounts.Add(p);
        AccountsEmpty = Accounts.Count == 0;
        AccountsHeading = Accounts.Count == 0 ? $"{who} accounts" : $"{who} accounts waiting ({Accounts.Count})";

        // ---- Change requests
        Requests.Clear();
        if (role == UserRole.ImsPersonnel)
        {
            RequestsExplanation = "Faculty requests approved by a Chairperson or Dean. Apply them to update the master schedule.";
            RequestsEmptyText = "No approved changes are waiting.";
            foreach (var r in await _changes.GetApprovedForImsAsync())
            {
                var by = r.ApproverRole == UserRole.Dean ? "Dean" : "Chairperson";
                Requests.Add(new ListRow(r.Id, $"{r.RequesterName}: {r.ScheduleLabel}",
                    $"{r.Summary}\nApproved by {by} {r.DecidedByName}",
                    r.Kind == ChangeKind.Room ? "Room change" : "Schedule change"));
            }
            RequestsHeading = Requests.Count == 0 ? "Approved changes" : $"Approved changes ({Requests.Count})";

            var open = await _changes.CountOpenAsync();
            PendingText = open == 0 ? "" : open == 1
                ? "1 request is still waiting for a Chairperson or Dean."
                : $"{open} requests are still waiting for a Chairperson or Dean.";
        }
        else
        {
            var dean = role == UserRole.Dean;
            RequestsExplanation = dean
                ? "Faculty asking to move a class into a room that belongs to your school. After you approve, IMS Personnel update the master schedule."
                : "Faculty of your department asking to change the day, time or subject of a class. After you approve, IMS Personnel update the master schedule.";
            RequestsEmptyText = "No requests are waiting for you.";
            foreach (var r in await _changes.GetPendingForMeAsync())
                Requests.Add(new ListRow(r.Id, $"{r.RequesterName}: {r.ScheduleLabel}", $"{r.Summary}\nReason: {r.Reason}",
                    $"Sent {r.CreatedUtc.ToLocalTime():MMM d, h:mm tt}"));
            var name = dean ? "Classroom change requests" : "Schedule change requests";
            RequestsHeading = Requests.Count == 0 ? name : $"{name} ({Requests.Count})";
            PendingText = "";
        }
        RequestsEmpty = Requests.Count == 0;
        HasPendingText = PendingText.Length > 0;

        var total = Accounts.Count + Requests.Count;
        Summary = total == 0 ? "Nothing is waiting for you."
            : total == 1 ? "1 item is waiting for you." : $"{total} items are waiting for you.";
    }

    // ------------------------------------------------------------- Accounts

    [RelayCommand]
    private Task ApproveAccountAsync(PendingApproval? item) => DecideAccountAsync(item, true);

    [RelayCommand]
    private async Task DeclineAccountAsync(PendingApproval? item)
    {
        if (item is null) return;
        var yes = await Shell.Current.DisplayAlertAsync("Decline registration",
            $"Decline {item.Name}? They will have to register again.", "Decline", "Cancel");
        if (yes) await DecideAccountAsync(item, false);
    }

    private async Task DecideAccountAsync(PendingApproval? item, bool approve)
    {
        if (item is null || IsBusy) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            var (ok, text) = await _approvals.DecideAsync(item.UserId, approve);
            if (ok) SuccessMessage = text; else ErrorMessage = text;
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    // ------------------------------------------------------------- Requests

    [RelayCommand]
    private Task ApproveRequestAsync(ListRow? row) => DecideRequestAsync(row, true);

    [RelayCommand]
    private async Task DeclineRequestAsync(ListRow? row)
    {
        if (row is null) return;
        var yes = await Shell.Current.DisplayAlertAsync("Decline request", $"Decline the request of {row.Title}?", "Decline", "Cancel");
        if (yes) await DecideRequestAsync(row, false);
    }

    private async Task DecideRequestAsync(ListRow? row, bool approve)
    {
        if (row is null || IsBusy) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _changes.DecideAsync(row.Id, approve));
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    /// <summary>IMS Personnel: apply an approved change to the master schedule.</summary>
    [RelayCommand]
    private async Task ApplyChangeAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _changes.ApplyAsync(row.Id));
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }
}
