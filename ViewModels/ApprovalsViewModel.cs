using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Chairperson: approve the Faculty of the department. Dean: approve the Chairpersons of the school.</summary>
public partial class ApprovalsViewModel : ObservableObject
{
    private readonly ApprovalService _approvals;
    private readonly SessionService _session;

    public ApprovalsViewModel(ApprovalService approvals, SessionService session)
    {
        _approvals = approvals;
        _session = session;
    }

    public ObservableCollection<PendingApproval> Items { get; } = new();

    [ObservableProperty] private string _heading = "Waiting for your approval";
    [ObservableProperty] private string _explanation = "";
    [ObservableProperty] private bool _isEmpty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool HasMessage => Message.Length > 0;
    public bool IsNotBusy => !IsBusy;

    public async Task LoadAsync()
    {
        var role = _session.Role;
        var who = role is null ? "accounts" : ApprovalService.WhatApproved(role.Value);
        Heading = $"{who} accounts waiting for approval";
        Explanation = role == UserRole.Dean
            ? "Chairpersons of your school who confirmed their email appear here. They cannot sign in until you approve them."
            : "Faculty of your department who confirmed their email appear here. They cannot sign in until you approve them.";

        Items.Clear();
        foreach (var p in await _approvals.GetPendingAsync()) Items.Add(p);
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private Task ApproveAsync(PendingApproval? item) => DecideAsync(item, true);

    [RelayCommand]
    private async Task DeclineAsync(PendingApproval? item)
    {
        if (item is null) return;
        var yes = await Shell.Current.DisplayAlertAsync("Decline registration",
            $"Decline {item.Name}? They will have to register again.", "Decline", "Cancel");
        if (yes) await DecideAsync(item, false);
    }

    private async Task DecideAsync(PendingApproval? item, bool approve)
    {
        if (item is null || IsBusy) return;
        try
        {
            IsBusy = true;
            var (_, text) = await _approvals.DecideAsync(item.UserId, approve);
            Message = text;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
