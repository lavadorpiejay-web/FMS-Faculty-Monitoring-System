using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar Home: today's duty hours (their Work Time) and the checks completed today. Checks are done on the Attendance tab.
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
    /// <summary>Title = subject and status, Detail = faculty and room, Extra = the checks done.</summary>
    public ObservableCollection<ListRow> Completed { get; } = new();

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private string _monitoringText = "";
    [ObservableProperty] private bool _hasNoDuty;
    [ObservableProperty] private bool _hasNoChecks;

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

        Completed.Clear();
        foreach (var c in await _attendance.GetTodayChecksAsync()) Completed.Add(c);
        HasNoChecks = Completed.Count == 0;
    }
}
