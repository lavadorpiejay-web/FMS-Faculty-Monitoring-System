using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// Home page of the three roles that approve things.
/// Chairperson: Faculty accounts + schedule change requests of the department.
/// Dean: Chairperson accounts + classroom change requests of the school's Faculty.
/// IMS Personnel: READS Scholar accounts + approved changes waiting for their availability check and final approval.
/// Every role also sees the resignations waiting for it (Chairperson: Faculty of the department, Dean: Chairpersons and Faculty of the school,
/// IMS Personnel: the ones forwarded by the Dean, who make them official).
/// All rules stay in ApprovalService, ChangeRequestService and ResignationService; this page only shows their lists and calls them.
/// </summary>
public partial class ApprovalsHomeViewModel : PageViewModelBase
{
    private readonly ApprovalService _approvals;
    private readonly ChangeRequestService _changes;
    private readonly ResignationService _resignations;
    private readonly SessionService _session;

    public ApprovalsHomeViewModel(ApprovalService approvals, ChangeRequestService changes, ResignationService resignations, SessionService session)
    {
        _approvals = approvals;
        _changes = changes;
        _resignations = resignations;
        _session = session;
    }

    public ObservableCollection<PendingApproval> Accounts { get; } = new();
    public ObservableCollection<ListRow> Requests { get; } = new();
    public ObservableCollection<ListRow> Resignations { get; } = new();

    [ObservableProperty] private string _welcome = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _accountsHeading = "Accounts waiting for approval";
    [ObservableProperty] private string _accountsExplanation = "";
    [ObservableProperty] private bool _accountsEmpty;
    [ObservableProperty] private string _requestsHeading = "Requests";
    [ObservableProperty] private string _requestsExplanation = "";
    [ObservableProperty] private string _requestsEmptyText = "";
    [ObservableProperty] private bool _requestsEmpty;
    [ObservableProperty] private string _resignationsHeading = "Resignations";
    [ObservableProperty] private string _resignationsExplanation = "";
    [ObservableProperty] private bool _resignationsEmpty;
    [ObservableProperty] private string _resignationApproveText = "Forward";
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
            UserRole.ImsPersonnel => "READS, and the Chairperson of the Center for Religious Education (CRE has no Dean), who confirmed their email. They cannot sign in until you approve them.",
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
            RequestsExplanation = "Faculty requests already approved by a Chairperson or Dean. Check the availability, then approve them to update the master schedule, or decline them.";
            RequestsEmptyText = "No approved changes are waiting.";
            foreach (var r in await _changes.GetApprovedForImsAsync())
            {
                var by = r.ApproverRole == UserRole.Dean ? "Dean" : "Chairperson";
                var check = await _changes.GetAvailabilityNoteAsync(r);
                Requests.Add(new ListRow(r.Id, $"{r.RequesterName}: {r.ScheduleLabel}",
                    $"{r.Summary}\nApproved by {by} {r.DecidedByName}\n{check}",
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
                ? "Faculty of your school asking to move a class into another room, and requests from other schools (or IMS Personnel) to use a room on a floor your school owns. After you approve a room change, IMS Personnel check the availability and give the final approval; an approved room permission saves the class straight away."
                : "Faculty of your department asking to change the day, time or subject of a class. After you approve, IMS Personnel check the availability and give the final approval.";
            RequestsEmptyText = "No requests are waiting for you.";
            foreach (var r in await _changes.GetPendingForMeAsync())
                Requests.Add(new ListRow(r.Id, $"{r.RequesterName}: {r.ScheduleLabel}", $"{r.Summary}\nReason: {r.Reason}",
                    $"Sent {r.CreatedUtc.ToLocalTime():MMM d, h:mm tt}"));
            var name = dean ? "Classroom requests" : "Schedule change requests";
            RequestsHeading = Requests.Count == 0 ? name : $"{name} ({Requests.Count})";
            PendingText = "";
        }
        RequestsEmpty = Requests.Count == 0;
        HasPendingText = PendingText.Length > 0;

        // ---- Resignations
        ResignationApproveText = role switch
        {
            UserRole.Chairperson => "Forward to Dean",
            UserRole.Dean => "Forward to IMS",
            _ => "Make official"
        };
        ResignationsExplanation = role switch
        {
            UserRole.Chairperson => "Faculty of your department who want to resign. Forward the resignation to your Dean, or decline it with a reason.",
            UserRole.Dean => "Chairpersons and Faculty of your school who want to resign, after their Chairperson forwarded them. Forward the resignation to IMS Personnel, or decline it with a reason.",
            _ => "Resignations forwarded by the Dean (a Dean's or a READS' own resignation comes straight to you). Making one official turns the account inactive, so the person can no longer sign in."
        };
        Resignations.Clear();
        foreach (var row in await _resignations.GetRowsForMeAsync()) Resignations.Add(row);
        ResignationsEmpty = Resignations.Count == 0;
        ResignationsHeading = Resignations.Count == 0 ? "Resignations" : $"Resignations waiting ({Resignations.Count})";

        var total = Accounts.Count + Requests.Count + Resignations.Count;
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

    // ------------------------------------------------------------- Resignations

    /// <summary>Chairperson and Dean forward the resignation; IMS Personnel make it official (the account becomes inactive).</summary>
    [RelayCommand]
    private async Task ApproveResignationAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        if (IsIms)
        {
            var yes = await Shell.Current.DisplayAlertAsync("Make resignation official",
                $"{row.Title}\n\nThe account becomes inactive and the person can no longer sign in. Continue?", "Make official", "Cancel");
            if (!yes) return;
        }
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _resignations.ApproveAsync(row.Id));
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeclineResignationAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        var reason = await Shell.Current.DisplayPromptAsync("Decline resignation",
            $"Why are you declining the resignation of {row.Title}? They will see this reason.", "Decline", "Cancel", "Reason", 200);
        if (reason is null) return;   // cancelled
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _resignations.DeclineAsync(row.Id, reason));
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

    /// <summary>IMS Personnel: decline an approved change (for example the room is not available).</summary>
    [RelayCommand]
    private async Task ImsDeclineAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        var reason = await Shell.Current.DisplayPromptAsync("Decline request", $"Why are you declining {row.Title}? The Faculty member will see this reason. Nothing will be changed in the schedule.",
            "Decline", "Cancel", "Reason", 200);
        if (reason is null) return;   // cancelled
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _changes.DeclineAsync(row.Id, reason));
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    /// <summary>IMS Personnel: give the final approval and apply an approved change to the master schedule.</summary>
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
