using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>IMS Personnel: encode the initial faculty schedules (faculty, subject, classroom, day, time) and remove mistakes.</summary>
public partial class SchedulesViewModel : PageViewModelBase
{
    private readonly ScheduleService _service;
    private Dictionary<string, UserAccount> _faculty = new();
    private List<Classroom> _rooms = new();
    private List<Subject> _subjects = new();
    private List<ClassSchedule> _allSchedules = new();
    private Dictionary<string, Classroom> _roomById = new();
    private int _patternMinutes;   // length of one meeting for the chosen subject's units (0 = no pattern)

    /// <summary>
    /// Weekly meeting pattern by subject units: the days the class meets and the minutes of each meeting.
    /// Choosing a subject fills the days and, once a start time is chosen, the end time. IMS can still change them.
    /// To add or change a rule, edit this table (units -> days, minutes per meeting).
    /// </summary>
    private static readonly Dictionary<int, (string[] Days, int Minutes)> UnitPatterns = new()
    {
        [3] = (new[] { "Monday", "Wednesday", "Friday" }, 60),    // 3 units: MWF, 1 hour each
        [2] = (new[] { "Tuesday", "Thursday" }, 90),              // 2 units: TTh, 1 hour 30 minutes each
    };

    public SchedulesViewModel(ScheduleService service)
    {
        _service = service;
        foreach (var name in ScheduleRules.DayNames)
        {
            var day = new DayChoice(name);
            day.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DayChoice.IsSelected)) RefreshCustomDays(); };
            Days.Add(day);
        }
    }

    /// <summary>The weekday tick boxes (Monday to Sunday).</summary>
    public ObservableCollection<DayChoice> Days { get; } = new();
    /// <summary>The ticked days, shown with their own time pickers when the days do not share one time.</summary>
    public ObservableCollection<DayChoice> CustomDays { get; } = new();

    public ObservableCollection<SchoolGroup> Schools { get; } = new();
    public IReadOnlyList<string> SchoolChoices { get; } = AcademicCatalog.SchoolNames;
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times.Select(TimeFormat.To12h).ToList();

    [ObservableProperty] private IReadOnlyList<string> _departmentChoices = Array.Empty<string>();
    [ObservableProperty] private string? _selectedSchool;
    [ObservableProperty] private string? _selectedDepartment;
    [ObservableProperty] private IReadOnlyList<string> _facultyLabels = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _subjectLabels = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _buildingChoices = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _floorChoices = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _roomChoices = Array.Empty<string>();
    [ObservableProperty] private string? _selectedBuilding;
    [ObservableProperty] private string? _selectedFloor;
    [ObservableProperty] private string? _selectedFaculty;
    [ObservableProperty] private string? _selectedSubject;
    [ObservableProperty] private string? _selectedRoom;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifferentTimes))]
    private bool _sameTime = true;
    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRows))]
    private bool _hasRows;

    public bool HasNoRows => !HasRows;

    /// <summary>When off, every ticked day gets its own start and end time.</summary>
    public bool DifferentTimes => !SameTime;

    partial void OnSameTimeChanged(bool value) => RefreshCustomDays();

    private void RefreshCustomDays()
    {
        CustomDays.Clear();
        if (SameTime) return;
        var picked = Days.Where(d => d.IsSelected).ToList();
        foreach (var d in picked)
        {
            // A day with no time yet starts from the shared time, so the first day's time can be copied and adjusted.
            d.Start ??= SelectedStart;
            d.End ??= SelectedEnd;
            CustomDays.Add(d);
        }
    }

    // Classroom: building first, then floor, then room (rooms are fixed, never typed).
    partial void OnSelectedBuildingChanged(string? value)
    {
        SelectedFloor = null;
        FloorChoices = value is null ? Array.Empty<string>()
            : _rooms.Where(r => r.Building == CampusLayout.BuildingCode(value)).Select(r => r.Floor).Distinct()
                    .OrderBy(FloorKey).ToList();
    }

    partial void OnSelectedFloorChanged(string? value)
    {
        SelectedRoom = null;
        RoomChoices = value is null || SelectedBuilding is null ? Array.Empty<string>()
            : _rooms.Where(r => r.Building == CampusLayout.BuildingCode(SelectedBuilding) && r.Floor == value)
                    .Select(r => r.RoomNo).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Floors sort as numbers; basements "B-1", "B-2" come first (lower basement before upper).
    private static (int, string) FloorKey(string floor)
    {
        if (int.TryParse(floor, out var n)) return (n, "");
        if (floor.StartsWith("B-", StringComparison.OrdinalIgnoreCase) && int.TryParse(floor[2..], out var b)) return (-b, "");
        return (int.MaxValue, floor);
    }

    private static int BuildingOrder(string code)
    {
        for (var i = 0; i < CampusLayout.MainCampus.Count; i++)
            if (CampusLayout.MainCampus[i].Code == code) return i;
        return int.MaxValue;
    }

    // Catalog order first (so schools and departments follow AcademicCatalog), anything unknown last.
    private static int CatalogOrder(IReadOnlyList<string> catalog, string name)
    {
        for (var i = 0; i < catalog.Count; i++)
            if (string.Equals(catalog[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return int.MaxValue;
    }

    private static string SubjectLabel(Subject s) => $"{s.Code} - {s.Title}";

    private Subject? ChosenSubject()
    {
        if (SelectedSubject is null || SelectedFaculty is null || !_faculty.TryGetValue(SelectedFaculty, out var f)) return null;
        var department = f.Role == UserRole.Dean ? SelectedDepartment : f.Department;
        return _subjects.FirstOrDefault(s => s.Department == department && SubjectLabel(s) == SelectedSubject);
    }

    // Choosing a subject applies the pattern for its units (days and meeting length).
    partial void OnSelectedSubjectChanged(string? value)
    {
        _patternMinutes = 0;
        var subject = ChosenSubject();
        if (subject is null || !UnitPatterns.TryGetValue(subject.Units, out var pattern)) return;

        _patternMinutes = pattern.Minutes;
        SameTime = true;
        foreach (var d in Days) d.IsSelected = pattern.Days.Contains(d.Name);
        ApplyPatternEnd();

        var length = pattern.Minutes % 60 == 0 ? $"{pattern.Minutes / 60} hour" : $"{pattern.Minutes / 60} hour {pattern.Minutes % 60} minutes";
        ClearMessages();
        Show(new OperationResult(true, $"{subject.Units}-unit subject: {string.Join(", ", pattern.Days)}, {length} each. You can still change the days and times."));
    }

    // With a pattern, choosing the start time fills the end time.
    partial void OnSelectedStartChanged(string? value) => ApplyPatternEnd();

    private void ApplyPatternEnd()
    {
        if (_patternMinutes == 0 || SelectedStart is null) return;
        var start = TimeFormat.To24h(SelectedStart);
        if (start is null) return;
        var end = ScheduleRules.Minutes(start) + _patternMinutes;
        if (end > 22 * 60) { SelectedEnd = null; return; }   // past the last time in the list: IMS chooses the end by hand
        SelectedEnd = TimeFormat.To12h(ScheduleRules.HHmmOf(end));
    }

    // Faculty are chosen school first, then department, then the person.
    partial void OnSelectedSchoolChanged(string? value)
    {
        SelectedDepartment = null;
        DepartmentChoices = value is null ? Array.Empty<string>() : AcademicCatalog.DepartmentsOf(value).ToList();
        RefreshFacultyLabels();
    }

    partial void OnSelectedDepartmentChanged(string? value)
    {
        SelectedFaculty = null;
        RefreshFacultyLabels();
    }

    private void RefreshFacultyLabels()
    {
        var labels = SelectedSchool is null || SelectedDepartment is null
            ? new List<string>()
            : _faculty.Where(kv => kv.Value.School == SelectedSchool
                                   && (kv.Value.Department == SelectedDepartment || kv.Value.Role == UserRole.Dean))   // a Dean has no department: listed under every department of the school
                      .Select(kv => kv.Key).ToList();
        if (!FacultyLabels.SequenceEqual(labels)) FacultyLabels = labels;
    }

    // The subject list follows the chosen Faculty member's department.
    partial void OnSelectedFacultyChanged(string? value)
    {
        SelectedSubject = null;
        SubjectLabels = value is not null && _faculty.TryGetValue(value, out var f)
            ? _subjects.Where(s => s.Department == (f.Role == UserRole.Dean ? SelectedDepartment : f.Department)).Select(SubjectLabel).ToList()
            : Array.Empty<string>();
    }

    public async Task LoadAsync()
    {
        _subjects = await _service.GetSubjectsAsync();

        _faculty = new Dictionary<string, UserAccount>();
        foreach (var u in await _service.GetFacultyAsync())
            _faculty.TryAdd(u.Role == UserRole.Faculty ? $"{u.FullName} ({u.SisId})" : $"{u.FullName} ({u.SisId}) - {u.Role}", u);
        RefreshFacultyLabels();

        var rooms = await _service.GetRoomsAsync();
        _rooms = rooms.Where(r => ScheduleRules.CampusOf(r) == AcademicCatalog.DefaultCampus).ToList();
        var buildings = _rooms.Select(r => r.Building).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(BuildingOrder).Select(CampusLayout.BuildingLabel).ToList();
        if (!BuildingChoices.SequenceEqual(buildings)) BuildingChoices = buildings;

        // Keep the open/closed state of each school and department across reloads.
        var openSchools = Schools.Where(g => g.IsExpanded).Select(g => g.Title).ToHashSet();
        var openDepts = Schools.SelectMany(g => g.Departments).Where(d => d.IsExpanded).Select(d => d.Key).ToHashSet();

        var byId = rooms.ToDictionary(r => r.Id);
        var schedules = await _service.GetVisibleAsync();
        _allSchedules = schedules;
        _roomById = byId;

        var schools = schedules
            .GroupBy(s => s.School.Length > 0 ? s.School : "(No school)")
            .OrderBy(g => CatalogOrder(AcademicCatalog.SchoolNames, g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var depts = g.GroupBy(s => s.Department.Length > 0 ? s.Department : "(No department)")
                    .OrderBy(d => CatalogOrder(AcademicCatalog.DepartmentsOf(g.Key), d.Key))
                    .ThenBy(d => d.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(d =>
                    {
                        var key = $"{g.Key}|{d.Key}";
                        // One row per class that meets at the same time in the same room: AC 2A1 on Monday, Wednesday and Friday at
                        // 11:00 shows once as "MWF 11:00 AM - 12:00 PM". A day with a different time or room gets its own row.
                        var rows = d.GroupBy(s => (s.FacultyUserId, s.SubjectCode, s.Start, s.End, s.ClassroomId))
                            .Select(grp =>
                            {
                                var s = grp.OrderBy(x => DayOrder(x.Day)).First();
                                byId.TryGetValue(s.ClassroomId, out var room);
                                return new ListRow(s.Id, $"{s.SubjectCode} - {s.SubjectTitle}",
                                    $"{s.FacultyName} | {ScheduleRules.DayLetters(grp.Select(x => x.Day))} {TimeFormat.Range(s.Start, s.End)} | {ScheduleRules.RoomLabel(room)}");
                            }).ToList();
                        return new RowGroup(key, d.Key, "", rows, openDepts.Contains(key));
                    }).ToList();
                return new SchoolGroup(g.Key, depts, openSchools.Contains(g.Key));
            }).ToList();

        Schools.Clear();
        foreach (var school in schools) Schools.Add(school);
        HasRows = Schools.Count > 0;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        if (SelectedFaculty is null || !_faculty.TryGetValue(SelectedFaculty, out var faculty)) { ErrorMessage = "Choose a Faculty member."; return; }
        if (SelectedSubject is null) { ErrorMessage = "Choose a subject."; return; }
        if (SelectedBuilding is null || SelectedFloor is null || SelectedRoom is null) { ErrorMessage = "Choose the building, floor and room."; return; }
        var buildingCode = CampusLayout.BuildingCode(SelectedBuilding);
        var room = _rooms.FirstOrDefault(r => r.Building == buildingCode && r.Floor == SelectedFloor && r.RoomNo == SelectedRoom);
        if (room is null) { ErrorMessage = "Choose a classroom."; return; }

        var picked = Days.Where(d => d.IsSelected).ToList();
        if (picked.Count == 0) { ErrorMessage = "Tick at least one day."; return; }
        // Same time for every day, or each day's own time (a day left blank follows the shared time).
        var days = picked.Select(d => (Day: ScheduleRules.ParseDay(d.Name),
            Start: TimeFormat.To24h(SameTime ? SelectedStart : (d.Start ?? SelectedStart)),
            End: TimeFormat.To24h(SameTime ? SelectedEnd : (d.End ?? SelectedEnd)))).ToList();

        var subjectDepartment = faculty.Role == UserRole.Dean ? SelectedDepartment : faculty.Department;
        var subject = _subjects.FirstOrDefault(s => s.Department == subjectDepartment && SubjectLabel(s) == SelectedSubject);
        if (subject is null) { ErrorMessage = "Choose a subject."; return; }

        try
        {
            IsBusy = true;
            var result = await _service.AddManyAsync(faculty.Id, subject.Code, room.Id, days);
            Show(result);
            if (result.Success) await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    private static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

    /// <summary>
    /// Remove asks what to delete: only that class, every class of the same subject, every class of that Faculty member on the same day,
    /// every class at the same time, all of that Faculty member's classes, or exactly the ones IMS ticks.
    /// The scope stays inside one Faculty member's classes so a single tap can never wipe other people's schedules.
    /// </summary>
    [RelayCommand]
    private async Task RemoveAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        var tapped = _allSchedules.FirstOrDefault(s => s.Id == row.Id);
        if (tapped is null) return;
        var theirs = _allSchedules.Where(s => s.FacultyUserId == tapped.FacultyUserId).ToList();

        string Line(ClassSchedule s) => $"{s.Day} {TimeFormat.Range(s.Start, s.End)}";

        // The tapped row stands for every day of the same subject, time and room (for example the Monday, Wednesday and Friday of AC 2A1).
        var group = theirs.Where(s => s.SubjectCode == tapped.SubjectCode && s.Start == tapped.Start
                                      && s.End == tapped.End && s.ClassroomId == tapped.ClassroomId)
                          .OrderBy(s => DayOrder(s.Day)).ToList();
        var scopes = new List<RemovePrompt.Scope>();
        if (group.Count > 1)
        {
            var letters = ScheduleRules.DayLetters(group.Select(s => s.Day));
            scopes.Add(new($"This schedule ({tapped.SubjectCode} {letters} {TimeFormat.Range(tapped.Start, tapped.End)}, {group.Count} days)", group.Select(s => s.Id).ToList()));
            foreach (var s in group) scopes.Add(new($"Only {s.Day} ({tapped.SubjectCode})", new[] { s.Id }));
        }
        else
        {
            scopes.Add(new($"Only this one ({tapped.SubjectCode}, {Line(tapped)})", new[] { tapped.Id }));
        }
        void Add(string label, IEnumerable<ClassSchedule> items)
        {
            var found = items.Select(s => s.Id).ToList();
            // Skip an option that would delete exactly what an earlier option already deletes.
            if (found.Count > 1 && !scopes.Any(x => x.Ids.Count == found.Count && !x.Ids.Except(found).Any()))
                scopes.Add(new($"{label} ({found.Count})", found));
        }
        Add($"All {tapped.SubjectCode} classes of {tapped.FacultyName}", theirs.Where(s => s.SubjectCode == tapped.SubjectCode));
        if (group.Count == 1) Add($"All classes of {tapped.FacultyName} on {tapped.Day}", theirs.Where(s => s.Day == tapped.Day));
        Add($"All classes of {tapped.FacultyName} at {TimeFormat.Range(tapped.Start, tapped.End)}", theirs.Where(s => s.Start == tapped.Start && s.End == tapped.End));
        Add($"Every class of {tapped.FacultyName}", theirs);

        var candidates = theirs.OrderBy(s => DayOrder(s.Day)).ThenBy(s => s.Start).Select(s =>
        {
            _roomById.TryGetValue(s.ClassroomId, out var room);
            return new ListRow(s.Id, $"{s.SubjectCode} - {s.SubjectTitle}", $"{Line(s)} | {ScheduleRules.RoomLabel(room)}");
        }).ToList();

        var ids = await RemovePrompt.AskAsync($"Remove schedule ({tapped.FacultyName})", "schedule", scopes, candidates);
        if (ids is null) return;

        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.RemoveManyAsync(ids));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
