using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>IMS Personnel: the master schedule with READS duty and monitoring assignments, plus attendance, irregularities and the audit trail.</summary>
public partial class MasterViewModel : PageViewModelBase
{
    private readonly ScheduleService _schedules;
    private readonly DutyService _duties;
    private readonly AttendanceService _attendance;
    private readonly AuditService _audit;
    private List<ClassSchedule> _all = new();
    private Dictionary<string, Classroom> _rooms = new();

    public MasterViewModel(ScheduleService schedules, DutyService duties, AttendanceService attendance, AuditService audit)
    {
        _schedules = schedules;
        _duties = duties;
        _attendance = attendance;
        _audit = audit;
        var filters = new List<string> { ScheduleRules.AllDepartments };
        filters.AddRange(AcademicCatalog.AllDepartments);
        DepartmentFilters = filters;
        SelectedDepartment = ScheduleRules.AllDepartments;
    }

    public IReadOnlyList<string> DepartmentFilters { get; }
    public ObservableCollection<ListRow> ScheduleRows { get; } = new();
    public ObservableCollection<ListRow> DutyRows { get; } = new();
    public ObservableCollection<ListRow> LogRows { get; } = new();
    public ObservableCollection<ListRow> IrregularityRows { get; } = new();
    public ObservableCollection<ListRow> AuditRows { get; } = new();

    [ObservableProperty] private string? _selectedDepartment;
    [ObservableProperty] private string _scheduleHeading = "Class schedules";

    partial void OnSelectedDepartmentChanged(string? value) => FillSchedules();

    private void FillSchedules()
    {
        ScheduleRows.Clear();
        foreach (var s in _all.Where(s => SelectedDepartment is null || SelectedDepartment == ScheduleRules.AllDepartments || s.Department == SelectedDepartment))
        {
            _rooms.TryGetValue(s.ClassroomId, out var room);
            ScheduleRows.Add(new ListRow(s.Id, $"{s.Day} {ScheduleRules.Range(s.Start, s.End)} | {s.SubjectCode}",
                $"{s.FacultyName} | {ScheduleRules.RoomLabel(room)}", s.Department));
        }
        ScheduleHeading = $"Class schedules ({ScheduleRows.Count})";
    }

    public async Task LoadAsync()
    {
        _all = await _schedules.GetVisibleAsync();
        _rooms = (await _schedules.GetRoomsAsync()).ToDictionary(r => r.Id);
        FillSchedules();

        DutyRows.Clear();
        foreach (var d in await _duties.GetOverviewAsync()) DutyRows.Add(new ListRow(d.Name, d.Name, $"{d.Assignment}\n{d.VacantSummary}"));

        LogRows.Clear();
        foreach (var l in await _attendance.GetRecentLogsForImsAsync(15))
            LogRows.Add(new ListRow(l.Id, $"{l.SubjectCode} - {l.Status} ({l.FacultyName})",
                $"{l.Date} {ScheduleRules.Range(l.ScheduledStart, l.ScheduledEnd)} | {l.RoomLabel} | checked by {l.CheckerName} at {l.TimestampUtc.ToLocalTime():h:mm tt}"));

        IrregularityRows.Clear();
        foreach (var r in await _attendance.GetIrregularitiesForImsAsync(15))
        {
            var kind = r.Kind switch
            {
                IrregularityKind.FacultyNotInRoom => "Faculty not in assigned room",
                IrregularityKind.UnapprovedRoom => "Class held in an unapproved room",
                _ => "Entry outside assignment"
            };
            var room = r.ObservedRoom.Length > 0 ? $" | seen in {r.ObservedRoom}" : "";
            var notes = r.Notes.Length > 0 ? $"\n{r.Notes}" : "";
            IrregularityRows.Add(new ListRow(r.Id, $"{(r.Flagged ? "FLAGGED: " : "")}{kind}",
                $"{r.FacultyName} | {r.ScheduleLabel} | expected {r.ExpectedRoom}{room}\nReported by {r.CheckerName} on {r.Date}{notes}"));
        }

        AuditRows.Clear();
        foreach (var a in await _audit.RecentAsync(25))
            AuditRows.Add(new ListRow(a.Id, a.Action, $"{a.Details}\n{a.ActorName} | {a.TimestampUtc.ToLocalTime():MMM d, h:mm tt}"));
    }
}
