using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// IMS Personnel: classrooms are fixed (Main Campus buildings MC, SN, SA, SJ). Pick a building, floor, day and time to see which rooms
/// are occupied or vacant according to the encoded schedules. IMS can also set the school assigned to a floor.
/// </summary>
public partial class RoomsViewModel : PageViewModelBase
{
    private readonly ScheduleService _service;
    private List<Classroom> _rooms = new();
    private List<ClassSchedule> _schedules = new();

    public RoomsViewModel(ScheduleService service)
    {
        _service = service;
        SelectedDay = DateTime.Today.DayOfWeek.ToString();
        SelectedStart = "8:00 AM";
        SelectedEnd = "9:00 AM";
    }

    public ObservableCollection<ListRow> RoomRows { get; } = new();
    public IReadOnlyList<string> Days { get; } = ScheduleRules.DayNames;
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times.Select(TimeFormat.To12h).ToList();
    public IReadOnlyList<string> Schools { get; } = AcademicCatalog.SchoolNames;

    [ObservableProperty] private IReadOnlyList<string> _buildingChoices = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _floorChoices = Array.Empty<string>();
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FloorSelected))] private string? _selectedBuilding;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FloorSelected))] private string? _selectedFloor;
    [ObservableProperty] private string? _selectedDay;
    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;
    [ObservableProperty] private string? _floorSchool;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRooms))]
    private bool _hasRooms;

    public bool HasNoRooms => !HasRooms;
    public bool FloorSelected => SelectedBuilding is not null && SelectedFloor is not null;

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

    partial void OnSelectedFloorChanged(string? value)
    {
        string? school = null;
        if (value is not null && SelectedBuilding is not null)
        {
            var room = _rooms.FirstOrDefault(r => r.Building == CampusLayout.BuildingCode(SelectedBuilding) && r.Floor == value);
            if (room is not null && room.School.Length > 0) school = room.School;
        }
        FloorSchool = school;
        Refresh();
    }

    partial void OnSelectedDayChanged(string? value) => Refresh();
    partial void OnSelectedStartChanged(string? value) => Refresh();
    partial void OnSelectedEndChanged(string? value) => Refresh();

    /// <summary>Lists the rooms of the chosen building (and floor) as Occupied or Vacant in the chosen day and time window.</summary>
    private void Refresh()
    {
        RoomRows.Clear();
        Summary = "";
        if (SelectedBuilding is null) { HasRooms = false; return; }

        var code = CampusLayout.BuildingCode(SelectedBuilding);
        var start = TimeFormat.To24h(SelectedStart);
        var end = TimeFormat.To24h(SelectedEnd);
        var windowOk = SelectedDay is not null && start is not null && end is not null && ScheduleRules.CheckTimes(start, end) is null;
        var day = windowOk ? ScheduleRules.ParseDay(SelectedDay!) : (DayOfWeek?)null;

        var rooms = _rooms.Where(r => r.Building == code && (SelectedFloor is null || r.Floor == SelectedFloor))
            .OrderBy(r => FloorKey(r.Floor)).ThenBy(r => r.RoomNo, StringComparer.OrdinalIgnoreCase).ToList();

        var occupied = 0;
        foreach (var r in rooms)
        {
            var clash = day is null ? null
                : _schedules.Where(s => s.ClassroomId == r.Id && s.Day == day && ScheduleRules.Overlaps(s.Start, s.End, start!, end!))
                            .OrderBy(s => s.Start).ToList();
            var floorText = $"Floor {r.Floor}";
            if (clash is null)
                RoomRows.Add(new ListRow(r.Id, r.RoomNo, floorText));
            else if (clash.Count == 0)
                RoomRows.Add(new ListRow(r.Id, $"{r.RoomNo}  -  Vacant", floorText));
            else
            {
                occupied++;
                var c = clash[0];
                RoomRows.Add(new ListRow(r.Id, $"{r.RoomNo}  -  Occupied",
                    $"{floorText} | {c.SubjectCode} ({c.FacultyName}), {TimeFormat.Range(c.Start, c.End)}"
                    + (clash.Count > 1 ? $" and {clash.Count - 1} more" : "")));
            }
        }

        HasRooms = RoomRows.Count > 0;
        Summary = day is null
            ? "Choose a day and a valid start and end time to see which rooms are occupied."
            : $"{occupied} occupied, {rooms.Count - occupied} vacant on {SelectedDay}, {TimeFormat.Range(start, end)}.";
    }

    [RelayCommand]
    private async Task SaveFloorSchoolAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        if (SelectedBuilding is null || SelectedFloor is null) { ErrorMessage = "Choose a building and a floor first."; return; }
        try
        {
            IsBusy = true;
            var result = await _service.SetFloorSchoolAsync(Campus, CampusLayout.BuildingCode(SelectedBuilding), SelectedFloor, FloorSchool);
            Show(result);
            if (!result.Success) return;
            var building = SelectedBuilding; var floor = SelectedFloor;
            await LoadAsync();
            SelectedBuilding = building; SelectedFloor = floor;
        }
        finally { IsBusy = false; }
    }
}
