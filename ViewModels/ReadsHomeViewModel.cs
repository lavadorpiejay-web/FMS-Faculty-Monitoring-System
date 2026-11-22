using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar Home: today's duty hours (their vacant times), the checks completed today, and a table of today's assigned classes
/// where the first check and the end check can be done directly, so the Scholar does not have to open the Attendance tab.
/// </summary>
public partial class ReadsHomeViewModel : PageViewModelBase
{
    private readonly AttendanceService _attendance;
    private readonly SessionService _session;
    private readonly ClockService _clock;

    public ReadsHomeViewModel(AttendanceService attendance, SessionService session, ClockService clock)
    {
        _attendance = attendance;
        _session = session;
        _clock = clock;
    }

    /// <summary>Title = time range, Detail = school monitored, Extra = Current / Upcoming / Completed.</summary>
    public ObservableCollection<ListRow> DutyHours { get; } = new();
    /// <summary>Today's classes of the assigned school inside the Scholar's vacant times, with the check that is open now (if any).</summary>
    public ObservableCollection<MonitorRow> Classes { get; } = new();
    /// <summary>Title = subject and status, Detail = faculty and room, Extra = the checks done.</summary>
    public ObservableCollection<ListRow> Completed { get; } = new();

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private string _monitoringText = "";
    [ObservableProperty] private bool _hasNoDuty;
    [ObservableProperty] private bool _hasNoChecks;
    [ObservableProperty] private bool _hasNoClasses;
    [ObservableProperty] private bool _hasClasses;
    [ObservableProperty] private string _classesEmptyText = "";
    [ObservableProperty] private string _checkNotes = "";

    public async Task LoadAsync()
    {
        var user = _session.CurrentUser;
        Greeting = user is null ? "Welcome" : $"Welcome, {user.FullName}";
        ClockText = ScheduleRules.ClockText(_clock.Now);

        var school = await _attendance.GetMonitoringSchoolAsync();
        MonitoringText = school.Length == 0 ? "Not assigned to a school yet" : $"Monitoring: {school}";

        DutyHours.Clear();
        foreach (var d in await _attendance.GetMyDutiesAsync()) DutyHours.Add(new ListRow(d.Id, d.Title, d.Detail, d.State));
        HasNoDuty = DutyHours.Count == 0;

        Classes.Clear();
        foreach (var row in await _attendance.GetMonitorRowsAsync()) Classes.Add(row);
        HasClasses = Classes.Count > 0;
        HasNoClasses = !HasClasses;
        var assignment = await _attendance.GetMyAssignmentInfoAsync();
        ClassesEmptyText = school.Length == 0
            ? "IMS Personnel have not assigned you to a school yet."
            : assignment.ClassCount == 0
                ? "IMS Personnel have not given you classes to check yet."
                : "None of your assigned classes are scheduled today.";

        Completed.Clear();
        foreach (var c in await _attendance.GetTodayChecksAsync()) Completed.Add(c);
        HasNoChecks = Completed.Count == 0;
    }

    [RelayCommand] private Task InRoomAsync(MonitorRow? row) => CheckAsync(row, true);
    [RelayCommand] private Task NotInRoomAsync(MonitorRow? row) => CheckAsync(row, false);

    // Same flow as the Attendance tab: confirm, save the check, then refresh the table.
    private async Task CheckAsync(MonitorRow? row, bool inRoom)
    {
        if (row is null || IsBusy) return;
        ClearMessages();
        var yes = await Shell.Current.DisplayAlertAsync(row.CheckCaption,
            $"{row.Title}\nFaculty is {(inRoom ? "IN" : "NOT IN")} the classroom?\n\nIt is timestamped and cannot be changed after you submit.",
            "Submit", "Cancel");
        if (!yes) return;

        try
        {
            IsBusy = true;
            var result = await _attendance.CheckAsync(row.ScheduleId, inRoom, CheckNotes);
            Show(result);
            if (result.Success) CheckNotes = "";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
