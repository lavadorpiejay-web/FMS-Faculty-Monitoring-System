using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// IMS Personnel: classrooms are fixed (Main Campus buildings MC, SN, SA, SJ) and belong to no school.
/// Pick a building, an optional floor, the days (tick boxes) and the time (one shared time or one per day, exactly like the Schedules tab)
/// to get a table of every room on every chosen day: Vacant, or Occupied with the Faculty, the subject and the school / department using it.
/// </summary>
public partial class RoomsViewModel : PageViewModelBase
{
    private const int MaxRows = 400;
    public const string AllRooms = "All rooms";

    private readonly ScheduleService _service;
    private List<Classroom> _rooms = new();
    private List<ClassSchedule> _schedules = new();
    private bool _suspend;

    public RoomsViewModel(ScheduleService service)
    {
        _service = service;
        foreach (var name in ScheduleRules.DayNames)
        {
            var day = new DayChoice(name);
            day.PropertyChanged += (_, e) =>
            {
                if (_suspend) return;
                if (e.PropertyName == nameof(DayChoice.IsSelected)) RefreshCustomDays();
                else if (e.PropertyName is nameof(DayChoice.Start) or nameof(DayChoice.End)) Refresh();
            };
            Days.Add(day);
        }

        _selectedStart = "8:00 AM";
        _selectedEnd = "9:00 AM";
        // Start with today ticked, as before.
        Days.First(d => d.Name == DateTime.Today.DayOfWeek.ToString()).IsSelected = true;
    }

    /// <summary>The weekday tick boxes (Monday to Sunday).</summary>
    public ObservableCollection<DayChoice> Days { get; } = new();
    /// <summary>The ticked days, shown with their own time pickers when the days do not share one time.</summary>
    public ObservableCollection<DayChoice> CustomDays { get; } = new();

    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times.Select(TimeFormat.To12h).ToList();

    [ObservableProperty] private IReadOnlyList<RoomAvailabilityRow> _table = Array.Empty<RoomAvailabilityRow>();
    [ObservableProperty] private IReadOnlyList<string> _buildingChoices = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _floorChoices = Array.Empty<string>();
    [ObservableProperty] private string? _selectedBuilding;
    [ObservableProperty] private IReadOnlyList<string> _roomChoices = Array.Empty<string>();
    [ObservableProperty] private string? _selectedFloor;
    [ObservableProperty] private string? _selectedRoom;
    [ObservableProperty] private bool _showVacant = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifferentTimes))]
    private bool _sameTime = true;
    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRooms))]
    private bool _hasRooms;

    public bool HasNoRooms => !HasRooms;

    /// <summary>When off, every ticked day gets its own start and end time.</summary>
    public bool DifferentTimes => !SameTime;

    // Floors sort as numbers ("2" before "10"). Basements "B-1", "B-2" come first, lower basement before upper basement.
    private static (int, string) FloorKey(string floor)
    {
        if (int.TryParse(floor, out var n)) return (n, "");
        if (floor.StartsWith("B-", StringComparison.OrdinalIgnoreCase) && int.TryParse(floor[2..], out var b)) return (-b, "");
        return (int.MaxValue, floor);
    }

    private static string Campus => AcademicCatalog.DefaultCampus;

    private static int BuildingOrder(string code)
    {
        for (var i = 0; i < CampusLayout.MainCampus.Count; i++)
            if (CampusLayout.MainCampus[i].Code == code) return i;
        return int.MaxValue;
    }

    public async Task LoadAsync()
    {
        _rooms = (await _service.GetRoomsAsync()).Where(r => ScheduleRules.CampusOf(r) == Campus).ToList();
        _schedules = await _service.GetVisibleAsync();

        var codes = _rooms.Select(r => r.Building).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(BuildingOrder).Select(CampusLayout.BuildingLabel).ToList();
        if (!BuildingChoices.SequenceEqual(codes)) BuildingChoices = codes;
        Refresh();
    }

    partial void OnSelectedBuildingChanged(string? value)
    {
        SelectedFloor = null;
        FloorChoices = value is null
            ? Array.Empty<string>()
            : _rooms.Where(r => r.Building == CampusLayout.BuildingCode(value)).Select(r => r.Floor).Distinct()
                    .OrderBy(FloorKey).ToList();
        Refresh();
    }

    // Building, then floor, then room: the table lists only what was chosen.
    partial void OnSelectedFloorChanged(string? value)
    {
        var rooms = value is null || SelectedBuilding is null ? new List<string>()
            : _rooms.Where(r => r.Building == CampusLayout.BuildingCode(SelectedBuilding) && r.Floor == value)
                    .Select(r => r.RoomNo).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        RoomChoices = value is null ? Array.Empty<string>() : new[] { AllRooms }.Concat(rooms).ToList();
        SelectedRoom = value is null ? null : AllRooms;
        Refresh();
    }

    partial void OnSelectedRoomChanged(string? value) => Refresh();
    partial void OnShowVacantChanged(bool value) => Refresh();
    partial void OnSelectedStartChanged(string? value) => Refresh();
    partial void OnSelectedEndChanged(string? value) => Refresh();
    partial void OnSameTimeChanged(bool value) => RefreshCustomDays();

    private void RefreshCustomDays()
    {
        _suspend = true;
        try
        {
            CustomDays.Clear();
            if (!SameTime)
            {
                foreach (var d in Days.Where(d => d.IsSelected))
                {
                    // A day with no time yet starts from the shared time, so it can be copied and adjusted.
                    d.Start ??= SelectedStart;
                    d.End ??= SelectedEnd;
                    CustomDays.Add(d);
                }
            }
        }
        finally { _suspend = false; }
        Refresh();
    }

    private void Clear(string summary)
    {
        Table = Array.Empty<RoomAvailabilityRow>();
        HasRooms = false;
        Summary = summary;
    }

    private static string SchoolDepartment(ClassSchedule s) =>
        string.Join("\n", new[] { s.School, s.Department }.Where(x => x.Length > 0));

    /// <summary>Builds the table: every room of the chosen building (and floor) on every ticked day, Vacant or Occupied in that day's time.</summary>
    private void Refresh()
    {
        if (SelectedBuilding is null) { Clear(""); return; }
        if (SelectedFloor is null) { Clear("Choose a floor to see its rooms."); return; }

        var picked = Days.Where(d => d.IsSelected).ToList();
        if (picked.Count == 0) { Clear("Tick at least one day."); return; }

        // Same time for every day, or each day's own time (a day left blank follows the shared time).
        var windows = new List<(DayOfWeek Day, string Name, string Start, string End)>();
        foreach (var d in picked)
        {
            var start = TimeFormat.To24h(SameTime ? SelectedStart : (d.Start ?? SelectedStart));
            var end = TimeFormat.To24h(SameTime ? SelectedEnd : (d.End ?? SelectedEnd));
            if (start is null || end is null) { Clear($"Choose a start and end time for {d.Name}."); return; }
            var error = ScheduleRules.CheckTimes(start, end);
            if (error is not null) { Clear($"{d.Name}: {error}"); return; }
            windows.Add((ScheduleRules.ParseDay(d.Name), d.Name, start, end));
        }

        var code = CampusLayout.BuildingCode(SelectedBuilding);
        var allRooms = SelectedRoom is null || SelectedRoom == AllRooms;
        var rooms = _rooms.Where(r => r.Building == code && r.Floor == SelectedFloor && (allRooms || r.RoomNo == SelectedRoom))
            .OrderBy(r => FloorKey(r.Floor)).ThenBy(r => r.RoomNo, StringComparer.OrdinalIgnoreCase).ToList();

        var rows = new List<RoomAvailabilityRow>();
        int occupied = 0, vacant = 0;
        foreach (var r in rooms)
        {
            var label = $"{r.Building} {r.RoomNo}";
            foreach (var w in windows)
            {
                var clash = _schedules
                    .Where(s => s.ClassroomId == r.Id && s.Day == w.Day && ScheduleRules.Overlaps(s.Start, s.End, w.Start, w.End))
                    .OrderBy(s => s.Start).ToList();
                if (clash.Count == 0)
                {
                    vacant++;
                    if (ShowVacant) rows.Add(new RoomAvailabilityRow(label, w.Name, TimeFormat.Range(w.Start, w.End), "Vacant", "-", "-", "-", true));
                    continue;
                }
                occupied++;
                foreach (var c in clash)
                    rows.Add(new RoomAvailabilityRow(label, w.Name, TimeFormat.Range(c.Start, c.End), "Occupied", c.FacultyName,
                        $"{c.SubjectCode} - {c.SubjectTitle}", SchoolDepartment(c), false));
            }
        }

        var summary = $"{occupied} room-day{(occupied == 1 ? "" : "s")} occupied, {vacant} vacant, for the chosen day and time.";
        if (rows.Count == 0) summary += " Nothing to show: every room is vacant (turn on \"Show vacant rooms\" to list them).";
        if (rows.Count > MaxRows)
        {
            rows = rows.Take(MaxRows).ToList();
            summary += $" Showing the first {MaxRows} rows; choose a floor or fewer days to narrow the table.";
        }
        Table = rows;
        HasRooms = rows.Count > 0;
        Summary = summary;
    }
}
