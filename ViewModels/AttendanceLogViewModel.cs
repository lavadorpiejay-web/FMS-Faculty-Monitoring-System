using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar: the classes of your assigned school that fall inside your vacant times, listed automatically.
/// Each class has two checks (5 minutes after the start, 5 minutes before the end): in the room or not in the room, with notes.
/// The status (Present / Late / Absent) follows from the two checks. Also irregularity reports. Debug builds have a test clock.
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
    [ObservableProperty] private string _monitoringText = "";
    [ObservableProperty] private string _emptyText = "";
    [ObservableProperty] private string _checkNotes = "";
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

        var school = await _service.GetMonitoringSchoolAsync();
        MonitoringText = school.Length == 0 ? "You are not assigned to a school yet." : $"Monitoring: {school}";
        EmptyText = school.Length == 0
            ? "IMS Personnel have not assigned you to a school yet."
            : "No classes of your assigned school fall inside your vacant times today. Enter your vacant times on the Duty tab.";

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

    [RelayCommand] private Task InRoomAsync(MonitorRow? row) => CheckAsync(row, true);
    [RelayCommand] private Task NotInRoomAsync(MonitorRow? row) => CheckAsync(row, false);

    private async Task CheckAsync(MonitorRow? row, bool inRoom)
    {
        if (row is null || IsBusy) return;
        ClearMessages();

        // Review and confirm before final submission. Present / Late / Absent is worked out from the two checks.
        var yes = await Shell.Current.DisplayAlertAsync(row.CheckCaption,
            $"{row.Title}\nFaculty is {(inRoom ? "IN" : "NOT IN")} the classroom?\n\nIt is timestamped and cannot be changed after you submit.",
            "Submit", "Cancel");
        if (!yes) return;

        try
        {
            IsBusy = true;
            var result = await _service.CheckAsync(row.ScheduleId, inRoom, CheckNotes);
            Show(result);
            if (result.Success) CheckNotes = "";
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
