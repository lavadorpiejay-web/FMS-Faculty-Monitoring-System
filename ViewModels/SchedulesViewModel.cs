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
    private Dictionary<string, Classroom> _rooms = new();
    private List<Subject> _subjects = new();

    public SchedulesViewModel(ScheduleService service) => _service = service;

    public ObservableCollection<SchoolGroup> Schools { get; } = new();
    public IReadOnlyList<string> Days { get; } = ScheduleRules.DayNames;
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times;

    [ObservableProperty] private IReadOnlyList<string> _facultyLabels = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _subjectLabels = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _roomLabels = Array.Empty<string>();
    [ObservableProperty] private string? _selectedFaculty;
    [ObservableProperty] private string? _selectedSubject;
    [ObservableProperty] private string? _selectedRoom;
    [ObservableProperty] private string? _selectedDay;
    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRows))]
    private bool _hasRows;

    public bool HasNoRows => !HasRows;

    // Catalog order first (so schools and departments follow AcademicCatalog), anything unknown last.
    private static int CatalogOrder(IReadOnlyList<string> catalog, string name)
    {
        for (var i = 0; i < catalog.Count; i++)
            if (string.Equals(catalog[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return int.MaxValue;
    }

    private static string SubjectLabel(Subject s) => $"{s.Code} - {s.Title}";

    // The subject list follows the chosen Faculty member's department.
    partial void OnSelectedFacultyChanged(string? value)
    {
        SelectedSubject = null;
        SubjectLabels = value is not null && _faculty.TryGetValue(value, out var f)
            ? _subjects.Where(s => s.Department == f.Department).Select(SubjectLabel).ToList()
            : Array.Empty<string>();
    }

    public async Task LoadAsync()
    {
        _subjects = await _service.GetSubjectsAsync();

        _faculty = new Dictionary<string, UserAccount>();
        foreach (var u in await _service.GetFacultyAsync()) _faculty.TryAdd($"{u.FullName} ({u.SisId})", u);
        var facultyLabels = _faculty.Keys.ToList();
        if (!FacultyLabels.SequenceEqual(facultyLabels)) FacultyLabels = facultyLabels;

        var rooms = await _service.GetRoomsAsync();
        _rooms = rooms.ToDictionary(r => ScheduleRules.RoomLabel(r));
        var roomLabels = _rooms.Keys.ToList();
        if (!RoomLabels.SequenceEqual(roomLabels)) RoomLabels = roomLabels;

        // Keep the open/closed state of each school and department across reloads.
        var openSchools = Schools.Where(g => g.IsExpanded).Select(g => g.Title).ToHashSet();
        var openDepts = Schools.SelectMany(g => g.Departments).Where(d => d.IsExpanded).Select(d => d.Key).ToHashSet();

        var byId = rooms.ToDictionary(r => r.Id);
        var schedules = await _service.GetVisibleAsync();

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
                        var rows = d.Select(s =>
                        {
                            byId.TryGetValue(s.ClassroomId, out var room);
                            return new ListRow(s.Id, $"{s.SubjectCode} - {s.SubjectTitle}",
                                $"{s.FacultyName} | {s.Day} {ScheduleRules.Range(s.Start, s.End)} | {ScheduleRules.RoomLabel(room)}");
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
        if (SelectedRoom is null || !_rooms.TryGetValue(SelectedRoom, out var room)) { ErrorMessage = "Choose a classroom."; return; }
        if (SelectedDay is null) { ErrorMessage = "Choose a day."; return; }

        var subject = _subjects.FirstOrDefault(s => s.Department == faculty.Department && SubjectLabel(s) == SelectedSubject);
        if (subject is null) { ErrorMessage = "Choose a subject."; return; }

        try
        {
            IsBusy = true;
            var result = await _service.AddAsync(faculty.Id, subject.Code, room.Id, SelectedDay, SelectedStart, SelectedEnd);
            Show(result);
            if (result.Success) await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        var yes = await Shell.Current.DisplayAlertAsync("Remove schedule", $"Remove {row.Title}?", "Remove", "Cancel");
        if (!yes) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.RemoveAsync(row.Id));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
