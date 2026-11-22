using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Chairperson: the Faculty of the Chairperson's own department (read-only).</summary>
public partial class DepartmentFacultyViewModel : ObservableObject
{
    private readonly DirectoryService _directory;
    private readonly ScheduleService _schedules;

    public DepartmentFacultyViewModel(DirectoryService directory, ScheduleService schedules)
    {
        _directory = directory;
        _schedules = schedules;
    }

    public ObservableCollection<FacultyWithSubjects> Faculty { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotValid))]
    private bool _isValid;

    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _facultyEmpty;

    public bool IsNotValid => !IsValid;

    public async Task LoadAsync()
    {
        var result = await _directory.LoadForCurrentChairpersonAsync();

        // The department's encoded schedules (a Chairperson sees their own department's), grouped per Faculty member and subject.
        var rooms = (await _schedules.GetRoomsAsync()).ToDictionary(r => r.Id);
        var schedules = await _schedules.GetVisibleAsync();
        static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

        Faculty.Clear();
        foreach (var f in result.Faculty)
        {
            var rows = new List<ListRow>();
            if (f.UserId.Length > 0)
                foreach (var g in schedules.Where(s => s.FacultyUserId == f.UserId).GroupBy(s => s.SubjectCode).OrderBy(g => g.Key))
                {
                    var lines = g.OrderBy(s => DayOrder(s.Day)).ThenBy(s => s.Start).Select(s =>
                    {
                        rooms.TryGetValue(s.ClassroomId, out var room);
                        return $"{s.Day} {TimeFormat.Range(s.Start, s.End)} | {ScheduleRules.RoomLabel(room)}";
                    });
                    rows.Add(new ListRow(g.Key, $"{g.Key} - {g.First().SubjectTitle}", string.Join("\n", lines)));
                }
            Faculty.Add(new FacultyWithSubjects(f.Name, rows));
        }

        School = result.School;
        Department = result.Department;
        FacultyEmpty = result.Faculty.Count == 0;
        Summary = result.Faculty.Count == 1 ? "1 faculty member" : $"{result.Faculty.Count} faculty members";
        IsValid = result.IsValid;
    }
}
