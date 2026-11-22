using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// Chairperson ("My schedule") and Dean ("My subjects"): the classes they handle themselves, as a weekly timetable and a list of subjects.
/// A Dean can handle GE subjects and major subjects of any department of the school, so the Dean's subjects are grouped by department.
/// Read-only: IMS Personnel encode the schedule.
/// </summary>
public partial class MyScheduleViewModel : PageViewModelBase
{
    private readonly ScheduleService _schedules;
    private readonly SessionService _session;

    public MyScheduleViewModel(ScheduleService schedules, SessionService session)
    {
        _schedules = schedules;
        _session = session;
    }

    public ObservableCollection<ListRow> Subjects { get; } = new();

    [ObservableProperty] private string _pageTitle = "My schedule";
    [ObservableProperty] private string _intro = "";
    [ObservableProperty] private string _emptyText = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private IReadOnlyList<TimetableBlock> _timetable = Array.Empty<TimetableBlock>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoClasses))]
    private bool _hasClasses;

    public bool HasNoClasses => !HasClasses;

    public async Task LoadAsync()
    {
        var dean = _session.Role == UserRole.Dean;
        PageTitle = dean ? "My subjects" : "My schedule";
        Intro = dean
            ? "The subjects you handle yourself, for example general education subjects for the school or major subjects of a department. IMS Personnel encode them. READS checkers monitor these classes like any other."
            : "The classes you handle yourself, besides leading your department. IMS Personnel encode them. READS checkers monitor these classes like any other.";
        EmptyText = dean ? "No subjects have been encoded for you yet." : "No classes have been encoded for you yet.";

        var rooms = (await _schedules.GetRoomsAsync()).ToDictionary(r => r.Id);
        var mine = await _schedules.GetMineAsync();
        HasClasses = mine.Count > 0;

        Timetable = mine.Select(s =>
        {
            rooms.TryGetValue(s.ClassroomId, out var room);
            return new TimetableBlock(s.Id, s.Day, s.Start, s.End, $"{s.SubjectCode} {s.SubjectTitle}",
                TimeFormat.Range(s.Start, s.End), room is null ? "" : $"Room {room.Building} {room.RoomNo}", s.SubjectCode);
        }).ToList();

        static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;
        Subjects.Clear();
        foreach (var g in mine.GroupBy(s => s.SubjectCode).OrderBy(g => g.Key))
        {
            var lines = g.OrderBy(s => DayOrder(s.Day)).ThenBy(s => s.Start).Select(s =>
            {
                rooms.TryGetValue(s.ClassroomId, out var room);
                return $"{s.Day} {TimeFormat.Range(s.Start, s.End)} | {ScheduleRules.RoomLabel(room)}";
            });
            var dept = g.First().Department;
            Subjects.Add(new ListRow(g.Key, $"{g.Key} - {g.First().SubjectTitle}", string.Join("\n", lines), dept));
        }
        var n = Subjects.Count;
        Summary = n == 1 ? "1 subject" : $"{n} subjects";
    }
}
