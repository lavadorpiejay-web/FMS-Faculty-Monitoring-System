using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Faculty: see your own schedule and request a schedule change (Chairperson) or a room change (Dean).</summary>
public partial class FacultyScheduleViewModel : PageViewModelBase
{
    private const string ScheduleKind = "Schedule change (approved by your Chairperson)";
    private const string RoomKind = "Room change (approved by the Dean, then IMS Personnel)";

    private readonly ScheduleService _schedules;
    private readonly ChangeRequestService _requests;
    private List<ClassSchedule> _mine = new();
    private List<Subject> _subjects = new();
    private Dictionary<string, Classroom> _rooms = new();
    private List<Classroom> _allRooms = new();

    public FacultyScheduleViewModel(ScheduleService schedules, ChangeRequestService requests)
    {
        _schedules = schedules;
        _requests = requests;
    }

    public ObservableCollection<ListRow> ScheduleRows { get; } = new();
    public ObservableCollection<ListRow> RequestRows { get; } = new();

    public IReadOnlyList<string> Kinds { get; } = new[] { ScheduleKind, RoomKind };
    public IReadOnlyList<string> DayChoices { get; } = ScheduleRules.DayChoicesWithNoChange;
    public IReadOnlyList<string> TimeChoices { get; } = ScheduleRules.TimesWithNoChange;

    [ObservableProperty] private IReadOnlyList<string> _classLabels = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _subjectChoices = new[] { ScheduleRules.NoChange };
    [ObservableProperty] private IReadOnlyList<string> _roomLabels = Array.Empty<string>();

    [ObservableProperty] private string? _selectedClass;
    [ObservableProperty] private string _selectedKind = ScheduleKind;
    [ObservableProperty] private string? _newDay = ScheduleRules.NoChange;
    [ObservableProperty] private string? _newStart = ScheduleRules.NoChange;
    [ObservableProperty] private string? _newEnd = ScheduleRules.NoChange;
    [ObservableProperty] private string? _newSubject = ScheduleRules.NoChange;
    [ObservableProperty] private string? _newRoom;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private bool _hasClasses;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoomChange))]
    private bool _isScheduleChange = true;

    public bool IsRoomChange => !IsScheduleChange;

    // The "Request a change" form stays hidden until the Faculty member taps its heading.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequestArrow))]
    [NotifyPropertyChangedFor(nameof(IsRequestClosed))]
    private bool _isRequestOpen;

    public bool IsRequestClosed => !IsRequestOpen;

    public string RequestArrow => IsRequestOpen ? "▼" : "▶";

    [RelayCommand]
    private void ToggleRequest()
    {
        ClearMessages();
        IsRequestOpen = !IsRequestOpen;
    }

    partial void OnSelectedClassChanged(string? value) => RebuildRoomChoices();

    /// <summary>
    /// New-room choices: only rooms of the building(s) the class's school is situated in
    /// (School of Engineering and Architecture = SA), and never the room the class already uses.
    /// </summary>
    private void RebuildRoomChoices()
    {
        var chosen = _mine.FirstOrDefault(s => ScheduleRules.ScheduleLabel(s) == SelectedClass);
        var school = chosen?.School ?? _mine.FirstOrDefault()?.School;
        var allowed = _allRooms
            .Where(r => CampusLayout.IsRoomAllowed(school, r) && r.Id != chosen?.ClassroomId)
            .OrderBy(r => r.Building).ThenBy(r => r.Floor).ThenBy(r => r.RoomNo)
            .ToList();
        _rooms = allowed.ToDictionary(r => ScheduleRules.RoomLabel(r));
        var labels = _rooms.Keys.ToList();
        if (!RoomLabels.SequenceEqual(labels)) RoomLabels = labels;
        if (NewRoom is not null && !_rooms.ContainsKey(NewRoom)) NewRoom = null;
    }

    partial void OnSelectedKindChanged(string value) => IsScheduleChange = value == ScheduleKind;

    public async Task LoadAsync()
    {
        _subjects = await _schedules.GetSubjectsAsync();
        var rooms = await _schedules.GetRoomsAsync();
        _allRooms = rooms.ToList();
        var byId = rooms.ToDictionary(r => r.Id);

        _mine = await _schedules.GetVisibleAsync();
        ScheduleRows.Clear();
        foreach (var s in _mine)
        {
            byId.TryGetValue(s.ClassroomId, out var room);
            ScheduleRows.Add(new ListRow(s.Id, $"{s.SubjectCode} - {s.SubjectTitle}",
                $"{s.Day} {ScheduleRules.Range(s.Start, s.End)} | {ScheduleRules.RoomLabel(room)}"));
        }
        HasClasses = ScheduleRows.Count > 0;
        var labels = _mine.Select(ScheduleRules.ScheduleLabel).ToList();
        if (!ClassLabels.SequenceEqual(labels)) ClassLabels = labels;
        RebuildRoomChoices();

        // Subjects of the Faculty member's own department can be requested.
        var dept = _mine.FirstOrDefault()?.Department ?? "";
        var subjectChoices = new[] { ScheduleRules.NoChange }.Concat(_subjects.Where(s => s.Department == dept).Select(s => s.Code)).ToList();
        if (!SubjectChoices.SequenceEqual(subjectChoices)) SubjectChoices = subjectChoices;

        RequestRows.Clear();
        foreach (var r in await _requests.GetMineAsync())
        {
            var decided = r.Status switch
            {
                ChangeStatus.Pending => r.Kind == ChangeKind.Room ? "waiting for the Dean" : "waiting for your Chairperson",
                ChangeStatus.Approved => "approved, IMS Personnel notified and will apply it",
                _ => r.Status.ToString().ToLowerInvariant()
            };
            RequestRows.Add(new ListRow(r.Id, $"{r.ScheduleLabel} - {decided}",
                $"{(r.Kind == ChangeKind.Room ? "Room" : "Schedule")} change: {r.Summary}\nReason: {r.Reason}" + (r.DeclineReason.Length > 0 ? $"\nDeclined because: {r.DeclineReason}" : ""),
                r.DecidedByName.Length > 0 ? $"Decided by {r.DecidedByName}" : ""));
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        var chosen = _mine.FirstOrDefault(s => ScheduleRules.ScheduleLabel(s) == SelectedClass);
        if (chosen is null) { ErrorMessage = "Choose the class you want to change."; return; }

        string? roomId = null;
        if (IsRoomChange)
        {
            if (NewRoom is null || !_rooms.TryGetValue(NewRoom, out var room)) { ErrorMessage = "Choose the new classroom."; return; }
            roomId = room.Id;
        }

        try
        {
            IsBusy = true;
            var result = await _requests.SubmitAsync(IsRoomChange ? ChangeKind.Room : ChangeKind.Schedule, chosen.Id,
                NewDay, NewStart, NewEnd, NewSubject, roomId, Reason);
            Show(result);
            if (!result.Success) return;
            Reason = "";
            NewDay = NewStart = NewEnd = NewSubject = ScheduleRules.NoChange;
            NewRoom = null;
            IsRequestOpen = false;   // fold the form away; the success message and "My requests" stay visible
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
