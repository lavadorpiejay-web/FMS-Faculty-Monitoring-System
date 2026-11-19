using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar: the classes inside your monitoring area and duty hours, digital attendance logging (Present / Late / Absent)
/// with a confirmation prompt, and irregularity reports. Debug builds also have a test clock.
/// </summary>
public partial class AttendanceLogViewModel : PageViewModelBase
{
    private const string NotInRoom = "Faculty not in the assigned room";
    private const string WrongRoom = "Class held in an unapproved room";

    private readonly AttendanceService _service;
    private readonly ScheduleService _schedules;
    private readonly ClockService _clock;
    private Dictionary<string, string> _classIds = new();    // report class label -> schedule id
    private Dictionary<string, Classroom> _rooms = new();

    public AttendanceLogViewModel(AttendanceService service, ScheduleService schedules, ClockService clock)
    {
        _service = service;
        _schedules = schedules;
        _clock = clock;
    }

    public ObservableCollection<MonitorRow> Rows { get; } = new();
    public ObservableCollection<ListRow> RecentLogs { get; } = new();

    public IReadOnlyList<string> ReportKinds { get; } = new[] { NotInRoom, WrongRoom };
    public IReadOnlyList<string> Days { get; } = ScheduleRules.DayNames;
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times;

#if DEBUG
    public bool IsDebug => true;
#else
    public bool IsDebug => false;
#endif

    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private bool _hasNoClasses;
    [ObservableProperty] private IReadOnlyList<string> _reportClassLabels = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _roomLabels = Array.Empty<string>();
    [ObservableProperty] private string? _selectedReportClass;
    [ObservableProperty] private string? _selectedReportKind;
    [ObservableProperty] private string? _selectedObservedRoom;
    [ObservableProperty] private string _notes = "";

    [ObservableProperty] private bool _useTestClock;
    [ObservableProperty] private string? _testDay;
    [ObservableProperty] private string? _testTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AskRoom))]
    private bool _isRoomReport;

    public bool AskRoom => IsRoomReport;

    partial void OnSelectedReportKindChanged(string? value) => IsRoomReport = value == WrongRoom;

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
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        ClockText = ScheduleRules.ClockText(_clock.Now) + (_clock.Override is null ? "" : "  (test clock)");

        var rooms = await _schedules.GetRoomsAsync();
        _rooms = rooms.ToDictionary(r => ScheduleRules.RoomLabel(r));
        var roomLabels = _rooms.Keys.ToList();
        if (!RoomLabels.SequenceEqual(roomLabels)) RoomLabels = roomLabels;

        Rows.Clear();
        foreach (var row in await _service.GetMonitorRowsAsync()) Rows.Add(row);
        HasNoClasses = Rows.Count == 0;

        _classIds = new Dictionary<string, string>();
        foreach (var row in Rows) _classIds.TryAdd(row.Title, row.ScheduleId);
        var labels = _classIds.Keys.ToList();
        if (!ReportClassLabels.SequenceEqual(labels)) ReportClassLabels = labels;

        RecentLogs.Clear();
        foreach (var l in await _service.GetMyLogsAsync(8)) RecentLogs.Add(l);
    }

    [RelayCommand] private Task PresentAsync(MonitorRow? row) => LogAsync(row, AttendanceStatus.Present);
    [RelayCommand] private Task LateAsync(MonitorRow? row) => LogAsync(row, AttendanceStatus.Late);
    [RelayCommand] private Task AbsentAsync(MonitorRow? row) => LogAsync(row, AttendanceStatus.Absent);

    private async Task LogAsync(MonitorRow? row, AttendanceStatus status)
    {
        if (row is null || IsBusy) return;
        ClearMessages();

        // Review and confirm before final submission.
        var yes = await Shell.Current.DisplayAlertAsync("Confirm attendance",
            $"{row.Title}\nMark as {status.ToString().ToUpperInvariant()}?\n\nIt is timestamped and cannot be changed after you submit.",
            "Submit", "Cancel");
        if (!yes) return;

        try
        {
            IsBusy = true;
            Show(await _service.LogAsync(row.ScheduleId, status));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ReportAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        if (SelectedReportClass is null || !_classIds.TryGetValue(SelectedReportClass, out var scheduleId))
        { ErrorMessage = "Choose the class you are reporting."; return; }
        if (SelectedReportKind is null) { ErrorMessage = "Choose what you observed."; return; }

        string? roomId = null;
        if (IsRoomReport)
        {
            if (SelectedObservedRoom is null || !_rooms.TryGetValue(SelectedObservedRoom, out var room))
            { ErrorMessage = "Choose the room where the class was held."; return; }
            roomId = room.Id;
        }

        try
        {
            IsBusy = true;
            var kind = IsRoomReport ? IrregularityKind.UnapprovedRoom : IrregularityKind.FacultyNotInRoom;
            var result = await _service.ReportAsync(kind, scheduleId, roomId, Notes);
            Show(result);
            if (result.Success) Notes = "";
        }
        finally { IsBusy = false; }
    }
}
