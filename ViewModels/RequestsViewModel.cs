using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Chairperson: approve Faculty schedule changes. Dean: approve classroom changes for the school's rooms.</summary>
public partial class RequestsViewModel : PageViewModelBase
{
    private readonly ChangeRequestService _service;
    private readonly SessionService _session;

    public RequestsViewModel(ChangeRequestService service, SessionService session)
    {
        _service = service;
        _session = session;
    }

    public ObservableCollection<ListRow> Rows { get; } = new();

    [ObservableProperty] private string _heading = "Change requests";
    [ObservableProperty] private string _explanation = "";
    [ObservableProperty] private bool _isEmpty;

    public async Task LoadAsync()
    {
        var dean = _session.Role == UserRole.Dean;
        Heading = dean ? "Classroom change requests" : "Schedule change requests";
        Explanation = dean
            ? "Faculty asking to move a class into a room that belongs to your school. After you approve, IMS Personnel update the master schedule."
            : "Faculty of your department asking to change the day, time or subject of a class. After you approve, IMS Personnel update the master schedule.";

        Rows.Clear();
        foreach (var r in await _service.GetPendingForMeAsync())
            Rows.Add(new ListRow(r.Id, $"{r.RequesterName}: {r.ScheduleLabel}", $"{r.Summary}\nReason: {r.Reason}",
                $"Sent {r.CreatedUtc.ToLocalTime():MMM d, h:mm tt}"));
        IsEmpty = Rows.Count == 0;
    }

    [RelayCommand]
    private Task ApproveAsync(ListRow? row) => DecideAsync(row, true);

    [RelayCommand]
    private async Task DeclineAsync(ListRow? row)
    {
        if (row is null) return;
        var yes = await Shell.Current.DisplayAlertAsync("Decline request", $"Decline the request of {row.Title}?", "Decline", "Cancel");
        if (yes) await DecideAsync(row, false);
    }

    private async Task DecideAsync(ListRow? row, bool approve)
    {
        if (row is null || IsBusy) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.DecideAsync(row.Id, approve));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
