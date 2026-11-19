using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>IMS Personnel: the approved schedule / room changes waiting to be applied to the master schedule.</summary>
public partial class ChangesViewModel : PageViewModelBase
{
    private readonly ChangeRequestService _service;

    public ChangesViewModel(ChangeRequestService service) => _service = service;

    public ObservableCollection<ListRow> Rows { get; } = new();

    [ObservableProperty] private string _heading = "Approved changes";
    [ObservableProperty] private string _pendingText = "";
    [ObservableProperty] private bool _isEmpty;

    public async Task LoadAsync()
    {
        Rows.Clear();
        var approved = await _service.GetApprovedForImsAsync();
        foreach (var r in approved)
        {
            var who = r.ApproverRole == UserRole.Dean ? "Dean" : "Chairperson";
            Rows.Add(new ListRow(r.Id, $"{r.RequesterName}: {r.ScheduleLabel}",
                $"{r.Summary}\nApproved by {who} {r.DecidedByName}", r.Kind == ChangeKind.Room ? "Room change" : "Schedule change"));
        }
        IsEmpty = Rows.Count == 0;
        Heading = Rows.Count == 0 ? "Approved changes" : $"Approved changes ({Rows.Count})";

        var open = await _service.CountOpenAsync();
        PendingText = open == 0 ? "No requests are waiting for a Chairperson or Dean."
            : open == 1 ? "1 request is still waiting for a Chairperson or Dean." : $"{open} requests are still waiting for a Chairperson or Dean.";
    }

    [RelayCommand]
    private async Task ApplyAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.ApplyAsync(row.Id));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
