using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar: the classes IMS Personnel assigned to you (inside your Work Time, on your floors), one card per class.
/// Each class has two checks (5 minutes after the start, last 15 minutes before the end): two buttons, In the Room or Not in the Room, with optional notes.
/// The status (Present / Late / Absent) follows from the two checks. Tapping either button opens a confirmation card (timestamped, cannot be changed) with a notes box;
/// Not in the Room also files the irregularity report for IMS, so there is no separate report form. Debug builds have a test clock.
/// </summary>
public partial class AttendanceLogViewModel : PageViewModelBase
{
    private readonly AttendanceService _service;
    private readonly ClockService _clock;
    private MonitorRow? _pending;
    private bool _pendingInRoom;

    public AttendanceLogViewModel(AttendanceService service, ClockService clock)
    {
        _service = service;
        _clock = clock;
    }

    public ObservableCollection<MonitorRow> Rows { get; } = new();
    public ObservableCollection<ListRow> RecentLogs { get; } = new();

    public IReadOnlyList<string> Days { get; } = ScheduleRules.DayNames;
    /// <summary>
    /// Test clock times for the READS Scholar: the moments a check can be done (5 minutes after a class starts, and inside the last 15 minutes before it ends),
    /// shown in 12-hour form, e.g. 8:05 AM and 8:25 AM for classes that start on the hour and end on the half hour.
    /// Only this screen uses them; schedules everywhere else keep their 30-minute steps.
    /// </summary>
    public IReadOnlyList<string> Times { get; } = BuildCheckTimes();

    private static List<string> BuildCheckTimes()
    {
        var list = new List<string>();
        for (var hour = 6; hour <= 21; hour++)
            foreach (var minute in new[] { 5, 15, 25, 35, 45, 55 })
                list.Add(TimeFormat.To12h($"{hour:00}:{minute:00}"));
        return list;
    }

#if DEBUG
    public bool IsDebug => true;
#else
    public bool IsDebug => false;
#endif

    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private string _monitoringText = "";
    [ObservableProperty] private string _floorsText = "";
    [ObservableProperty] private bool _hasFloors;
    [ObservableProperty] private string _emptyText = "";
    [ObservableProperty] private string _checkNotes = "";
    [ObservableProperty] private bool _hasNoClasses;
    [ObservableProperty] private bool _hasClasses;
    [ObservableProperty] private string _progressText = "";

    // Confirmation card (shown after tapping In the Room / Not in the Room).
    [ObservableProperty] private bool _isConfirming;
    [ObservableProperty] private string _confirmTitle = "";
    [ObservableProperty] private string _confirmText = "";
    [ObservableProperty] private string _confirmButtonText = "Submit";

    [ObservableProperty] private bool _useTestClock;
    [ObservableProperty] private string? _testDay;
    [ObservableProperty] private string? _testTime;

    partial void OnUseTestClockChanged(bool value) => _ = ApplyTestClockAsync();
    partial void OnTestDayChanged(string? value) => _ = ApplyTestClockAsync();
    partial void OnTestTimeChanged(string? value) => _ = ApplyTestClockAsync();

    private async Task ApplyTestClockAsync()
    {
        if (UseTestClock && TestDay is not null && TestTime is not null)
        {
            var target = ScheduleRules.ParseDay(TestDay);
            var date = DateTime.Today.AddDays(((int)target - (int)DateTime.Today.DayOfWeek + 7) % 7);
            var hhmm = TimeFormat.To24h(TestTime);
            if (hhmm is null) { _clock.Override = null; await LoadAsync(); return; }
            _clock.Override = date.AddMinutes(ScheduleRules.Minutes(hhmm));
        }
        else _clock.Override = null;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        ClockText = ScheduleRules.ClockText(_clock.Now) + (_clock.Override is null ? "" : "  (test clock)");

        var school = await _service.GetMonitoringSchoolAsync();
        MonitoringText = school.Length == 0 ? "You are not assigned to a school yet." : $"Monitoring: {school}";
        var assignment = await _service.GetMyAssignmentInfoAsync();
        HasFloors = assignment.ClassCount > 0;
        FloorsText = HasFloors ? $"Your floors: {assignment.Floors}" : "";
        EmptyText = school.Length == 0
            ? "IMS Personnel have not assigned you to a school yet."
            : assignment.ClassCount == 0
                ? "IMS Personnel have not given you classes to check yet. Enter your Work Time on the Duty tab so they can assign classes inside them."
                : "None of your assigned classes are scheduled today.";

        Rows.Clear();
        foreach (var row in await _service.GetMonitorRowsAsync()) Rows.Add(row);
        HasNoClasses = Rows.Count == 0;
        HasClasses = !HasNoClasses;
        var finished = Rows.Count(r => r.IsFinished);
        ProgressText = $"Finished {finished} of {Rows.Count} class{(Rows.Count == 1 ? "" : "es")} today";

        RecentLogs.Clear();
        foreach (var l in await _service.GetMyLogsAsync(8)) RecentLogs.Add(l);
    }

    [RelayCommand] private void InRoom(MonitorRow? row) => BeginConfirm(row, true);
    [RelayCommand] private void NotInRoom(MonitorRow? row) => BeginConfirm(row, false);

    private void BeginConfirm(MonitorRow? row, bool inRoom)
    {
        if (row is null || IsBusy) return;
        ClearMessages();
        _pending = row;
        _pendingInRoom = inRoom;
        CheckNotes = "";
        ConfirmTitle = $"{row.CheckCaption}: faculty is {(inRoom ? "IN" : "NOT IN")} the room";
        ConfirmText = $"{row.Title}\n\nThis entry is timestamped and cannot be changed after you submit."
                      + (inRoom ? "" : "\nIMS Personnel will also receive an irregularity report.");
        ConfirmButtonText = inRoom ? "Submit: In the Room" : "Submit: Not in the Room";
        IsConfirming = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        _pending = null;
        CheckNotes = "";
        IsConfirming = false;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (_pending is null || IsBusy) return;
        ClearMessages();
        try
        {
            IsBusy = true;
            var result = await _service.CheckAsync(_pending.ScheduleId, _pendingInRoom, CheckNotes);
            Show(result);
            Cancel();
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
