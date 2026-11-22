using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Directory, one person (Dean, Chairperson or Faculty): all their information, their subjects and schedule.
/// For a Faculty member it also lists today's classes (the same checks as the Attendance tab) and the checker's own notes,
/// which can be edited. Real life: students say the instructor is on a bathroom break or in a meeting with the Chairperson,
/// so the checker can note that the faculty is present although not in the room (or mark Late / Absent) right here.
/// </summary>
public partial class PersonDetailViewModel : PageViewModelBase
{
    private readonly DirectoryService _directory;
    private readonly AttendanceService _attendance;
    private DirectoryEntry? _entry;

    public PersonDetailViewModel(DirectoryService directory, AttendanceService attendance)
    {
        _directory = directory;
        _attendance = attendance;
    }

    public ObservableCollection<ListRow> Subjects { get; } = new();
    public ObservableCollection<ListRow> Related { get; } = new();
    public ObservableCollection<MonitorRow> TodayClasses { get; } = new();
    public ObservableCollection<ListRow> Notes { get; } = new();

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _roleText = "";
    [ObservableProperty] private string _sisId = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private bool _isFaculty;
    [ObservableProperty] private bool _showSubjects;
    [ObservableProperty] private string _subjectsHeading = "";
    [ObservableProperty] private string _noSubjectsText = "";
    [ObservableProperty] private bool _hasNoSubjects;
    [ObservableProperty] private bool _hasRelated;
    [ObservableProperty] private string _relatedHeading = "";
    [ObservableProperty] private bool _hasTodayClasses;
    [ObservableProperty] private bool _hasNoTodayClasses;
    [ObservableProperty] private bool _hasNotes;
    [ObservableProperty] private bool _hasNoNotes;
    [ObservableProperty] private string _checkNotes = "";

    public void SetPerson(DirectoryEntry entry)
    {
        _entry = entry;
        Name = entry.Name;
    }

    public async Task LoadAsync()
    {
        if (_entry is null) return;
        var detail = await _directory.LoadPersonAsync(_entry);
        if (!detail.IsValid)
        {
            ErrorMessage = "This person is not in your assigned school.";
            return;
        }

        Name = detail.Name;
        RoleText = detail.RoleText;
        SisId = detail.SisId.Length > 0 ? detail.SisId : "Not provided";
        Email = detail.Email.Length > 0 ? detail.Email : "Not provided";
        School = detail.School.Length > 0 ? detail.School : "Not provided";
        Department = detail.Department.Length > 0 ? detail.Department : "Not applicable";
        IsFaculty = detail.IsFaculty;

        SubjectsHeading = detail.SubjectsHeading;
        ShowSubjects = detail.SubjectsHeading.Length > 0;
        NoSubjectsText = "No classes have been encoded for this person yet.";
        Fill(Subjects, detail.Subjects);
        HasNoSubjects = Subjects.Count == 0;

        RelatedHeading = detail.RelatedHeading;
        Fill(Related, detail.Related);
        HasRelated = Related.Count > 0;

        TodayClasses.Clear();
        Notes.Clear();
        if (detail.IsFaculty && _entry.UserId.Length > 0)
        {
            foreach (var row in (await _attendance.GetMonitorRowsAsync()).Where(r => r.FacultyUserId == _entry.UserId))
                TodayClasses.Add(row);
            foreach (var n in await _attendance.GetNotesForFacultyAsync(_entry.UserId)) Notes.Add(n);
        }
        HasTodayClasses = TodayClasses.Count > 0;
        HasNoTodayClasses = !HasTodayClasses;
        HasNotes = Notes.Count > 0;
        HasNoNotes = !HasNotes;
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    // ------------------------------------------------------------ checks

    [RelayCommand] private Task InRoomAsync(MonitorRow? row) => CheckAsync(row, true);
    [RelayCommand] private Task NotInRoomAsync(MonitorRow? row) => CheckAsync(row, false);

    // Same flow and rules as the Attendance tab: confirm, save the check, refresh.
    private async Task CheckAsync(MonitorRow? row, bool inRoom)
    {
        if (row is null || IsBusy) return;
        ClearMessages();
        var yes = await Shell.Current.DisplayAlertAsync(row.CheckCaption,
            $"{row.Title}\nFaculty is {(inRoom ? "IN" : "NOT IN")} the classroom?\n\nIt is timestamped and cannot be changed after you submit.",
            "Submit", "Cancel");
        if (!yes) return;

        try
        {
            IsBusy = true;
            var result = await _attendance.CheckAsync(row.ScheduleId, inRoom, CheckNotes);
            Show(result);
            if (result.Success) CheckNotes = "";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    // ------------------------------------------------- notes and status

    [RelayCommand] private Task AdjustAsync(MonitorRow? row) => row is null ? Task.CompletedTask : EditAsync(row.LogId, row.Title);
    [RelayCommand] private Task EditNoteAsync(ListRow? row) => row is null ? Task.CompletedTask : EditAsync(row.Id, row.Title);

    /// <summary>Choose what to change (the note, or the status with a reason), then type the note. Saved to the same attendance entry.</summary>
    private async Task EditAsync(string logId, string title)
    {
        if (logId.Length == 0 || IsBusy) return;
        ClearMessages();

        const string noteOnly = "Edit the note only";
        const string bathroom = "Present: bathroom break";
        const string meeting = "Present: in a meeting with the Chairperson";
        const string presentOther = "Present: other reason";
        const string late = "Late";
        const string absent = "Absent";

        var choice = await Shell.Current.DisplayActionSheetAsync(title, "Cancel", null,
            noteOnly, bathroom, meeting, presentOther, late, absent);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        AttendanceStatus? status = choice switch
        {
            bathroom or meeting or presentOther => AttendanceStatus.Present,
            late => AttendanceStatus.Late,
            absent => AttendanceStatus.Absent,
            _ => null
        };
        var reason = choice == bathroom ? "Faculty is on a bathroom break"
                   : choice == meeting ? "Faculty is in a meeting with the Chairperson"
                   : "";

        var current = await _attendance.GetLogNotesAsync(logId);
        var initial = current.Length == 0 ? reason : reason.Length == 0 ? current : $"{current} | {reason}";
        var text = await Shell.Current.DisplayPromptAsync(
            status is null ? "Edit note" : $"Mark as {status}",
            "Write what you observed or were told (for example, students said the instructor is on a bathroom break).",
            "Save", "Cancel", "Note", -1, null, initial);
        if (text is null) return;   // cancelled

        try
        {
            IsBusy = true;
            var result = await _attendance.UpdateLogAsync(logId, status, reason.Length > 0 ? reason : text, text);
            Show(result);
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
