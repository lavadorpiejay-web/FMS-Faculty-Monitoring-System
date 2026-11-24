using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar: the classes inside your monitoring area and duty hours, and digital attendance logging.
/// Tapping In the Room or Not in the Room opens a confirmation (timestamped, cannot be changed) with a notes box;
/// Not in the Room also files the irregularity report for IMS. Debug builds also have a test clock.
/// </summary>
public partial class AttendanceLogViewModel : PageViewModelBase
{
    private readonly AttendanceService _service;
    private readonly ClockService _clock;
    private MonitorRow? _pending;

    public AttendanceLogViewModel(AttendanceService service, ClockService clock)
    {
        _service = service;
        _clock = clock;
    }

    public ObservableCollection<MonitorRow> Rows { get; } = new();
    public ObservableCollection<ListRow> RecentLogs { get; } = new();

    public IReadOnlyList<string> Days { get; } = ScheduleRules.DayNames;
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times;

#if DEBUG
    public bool IsDebug => true;
#else
    public bool IsDebug => false;
#endif

    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private bool _hasNoClasses;

    [ObservableProperty] private bool _useTestClock;
    [ObservableProperty] private string? _testDay;
    [ObservableProperty] private string? _testTime;

    // Confirmation form (shown after tapping In the Room / Not in the Room).
    [ObservableProperty] private bool _isConfirming;
    [ObservableProperty] private string _confirmTitle = "";
    [ObservableProperty] private string _confirmText = "";
    [ObservableProperty] private string _confirmButtonText = "Submit";
    [ObservableProperty] private string _notes = "";
    private AttendanceStatus _pendingStatus;

    partial void OnUseTestClockChanged(bool value) => _ = ApplyTestClockAsync();
    partial void OnTestDayChanged(string? value) => _ = ApplyTestClockAsync();
    partial void OnTestTimeChanged(string? value) => _ = ApplyTestClockAsync();

    private async Task ApplyTestClockAsync()
    {
        if (UseTestClock && TestDay is not null && TestTime is not null)
        {
            var target = ScheduleRules.ParseDay(TestDay);
            var date = DateTime.Today.AddDays(((int)target - (int)DateTime.Today.DayOfWeek + 7) % 7);
            _clock.Override = date.AddMinutes(ScheduleRules.Minutes(TestTime));
        }
        else _clock.Override = null;
        Cancel();
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        ClockText = ScheduleRules.ClockText(_clock.Now) + (_clock.Override is null ? "" : "  (test clock)");

        Rows.Clear();
        foreach (var row in await _service.GetMonitorRowsAsync()) Rows.Add(row);
        HasNoClasses = Rows.Count == 0;

        RecentLogs.Clear();
        foreach (var l in await _service.GetMyLogsAsync(8)) RecentLogs.Add(l);
    }

    [RelayCommand] private void InRoom(MonitorRow? row) => BeginConfirm(row, AttendanceStatus.Present);
    [RelayCommand] private void NotInRoom(MonitorRow? row) => BeginConfirm(row, AttendanceStatus.Absent);

    private void BeginConfirm(MonitorRow? row, AttendanceStatus status)
    {
        if (row is null || IsBusy) return;
        ClearMessages();
        _pending = row;
        _pendingStatus = status;
        Notes = "";
        var inRoom = status == AttendanceStatus.Present;
        ConfirmTitle = inRoom ? "Faculty is IN the room" : "Faculty is NOT in the room";
        ConfirmText = $"{row.Title}\n{row.Detail}\n\nThis entry is timestamped and cannot be changed after you submit."
                      + (inRoom ? "" : "\nIMS Personnel will also receive an irregularity report.");
        ConfirmButtonText = inRoom ? "Submit: In the Room" : "Submit: Not in the Room";
        IsConfirming = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        _pending = null;
        Notes = "";
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
            var result = await _service.LogAsync(_pending.ScheduleId, _pendingStatus, Notes);
            Show(result);
            Cancel();
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
