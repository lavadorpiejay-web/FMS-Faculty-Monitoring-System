using System.Text.Json;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Class schedules and classroom records. IMS Personnel encode them; every role sees the part that concerns it.</summary>
public sealed class ScheduleService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly ScholarAssignmentService _scholars;

    public ScheduleService(JsonStorageService storage, SessionService session, AuditService audit,
        NotificationService notifications, ScholarAssignmentService scholars)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
        _notifications = notifications;
        _scholars = scholars;
    }

    private bool IsIms => _session.Role == UserRole.ImsPersonnel;
    private static OperationResult Fail(string message) => new(false, message);

    public async Task<List<Classroom>> GetRoomsAsync()
    {
        await _storage.InitializeAsync();
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        return rooms.OrderBy(ScheduleRules.CampusOf).ThenBy(r => r.Building).ThenBy(r => r.RoomNo).ToList();
    }

    public async Task<List<Subject>> GetSubjectsAsync()
    {
        await _storage.InitializeAsync();
        return await _storage.LoadAsync<Subject>(JsonStorageService.SubjectsFile);
    }

    /// <summary>IMS: all schedules. Faculty: own. Chairperson: own department. Dean: own school. Others: none.</summary>
    public async Task<List<ClassSchedule>> GetVisibleAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return new();
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var visible = me.Role switch
        {
            UserRole.ImsPersonnel => all,
            UserRole.Faculty => all.Where(s => s.FacultyUserId == me.Id).ToList(),
            UserRole.Chairperson => all.Where(s => s.School == me.School && s.Department == me.Department).ToList(),
            UserRole.Dean => all.Where(s => s.School == me.School).ToList(),
            _ => new List<ClassSchedule>()
        };
        return visible.OrderBy(s => s.Day == DayOfWeek.Sunday ? 7 : (int)s.Day).ThenBy(s => s.Start).ThenBy(s => s.SubjectCode).ToList();
    }

    private static bool CanTeach(UserAccount u) => u.IsUsable && u.Role is UserRole.Faculty or UserRole.Chairperson or UserRole.Dean;

    /// <summary>The subject a person can be scheduled for: their own department's, or (a Dean has no department) any department of their school.</summary>
    private static bool SubjectFits(Subject s, UserAccount teacher, string code) =>
        s.Code == code && (teacher.Role == UserRole.Dean
            ? AcademicCatalog.DepartmentsOf(teacher.School).Contains(s.Department)
            : s.Department == teacher.Department);

    /// <summary>IMS only: the accounts a schedule can be encoded for (Faculty, and Chairpersons and Deans who also handle subjects).</summary>
    public async Task<List<UserAccount>> GetFacultyAsync()
    {
        if (!IsIms) return new();
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return users.Where(CanTeach).OrderBy(u => u.FullName).ToList();
    }

    /// <summary>The classes the signed-in person handles themselves (any role that teaches: Faculty, Chairperson, Dean).</summary>
    public async Task<List<ClassSchedule>> GetMineAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return new();
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        return all.Where(s => s.FacultyUserId == me.Id)
            .OrderBy(s => s.Day == DayOfWeek.Sunday ? 7 : (int)s.Day).ThenBy(s => s.Start).ToList();
    }

    /// <summary>The first clash with the candidate: same room or same Faculty at an overlapping time on the same day.</summary>
    public static string? FindConflict(IEnumerable<ClassSchedule> all, ClassSchedule candidate)
    {
        foreach (var s in all.Where(s => s.Id != candidate.Id && s.Day == candidate.Day
                                         && ScheduleRules.Overlaps(s.Start, s.End, candidate.Start, candidate.End)))
        {
            if (s.ClassroomId == candidate.ClassroomId)
                return $"That room is already used by {s.SubjectCode} ({s.FacultyName}) on {s.Day}, {TimeFormat.Range(s.Start, s.End)}.";
            if (s.FacultyUserId == candidate.FacultyUserId)
                return $"{candidate.FacultyName} already teaches {s.SubjectCode} on {s.Day}, {TimeFormat.Range(s.Start, s.End)}.";
        }
        return null;
    }

    // ------------------------------------------------------------------ encoding new classes

    private sealed record Prepared(UserAccount Faculty, Subject Subject, Classroom Room, List<ClassSchedule> Candidates);

    /// <summary>Checks everything about a new class (times, person, subject, room, clashes) and builds its rows. Nothing is saved.</summary>
    private async Task<(Prepared? Ok, string Error)> PrepareAsync(string facultyUserId, string subjectCode, string classroomId,
        IReadOnlyList<(DayOfWeek Day, string? Start, string? End)> days)
    {
        if (days.Count == 0) return (null, "Choose at least one day.");
        foreach (var d in days)
        {
            var timeError = ScheduleRules.CheckTimes(d.Start, d.End);
            if (timeError is not null) return (null, $"{d.Day}: {timeError}");
        }

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var faculty = users.FirstOrDefault(u => u.Id == facultyUserId && CanTeach(u));
        if (faculty is null) return (null, "That Faculty member was not found.");

        var subject = (await GetSubjectsAsync()).FirstOrDefault(s => SubjectFits(s, faculty, subjectCode));
        if (subject is null) return (null, "Choose a subject of the person's department (a Dean: a department of the school).");

        var room = (await GetRoomsAsync()).FirstOrDefault(r => r.Id == classroomId);
        if (room is null) return (null, "Choose a classroom.");

        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var added = new List<ClassSchedule>();
        foreach (var d in days)
        {
            var candidate = new ClassSchedule
            {
                Id = ScheduleRules.NewId("S"), FacultyUserId = faculty.Id, FacultyName = faculty.FullName,
                School = faculty.School, Department = faculty.Role == UserRole.Dean ? subject.Department : faculty.Department,
                SubjectCode = subject.Code, SubjectTitle = subject.Title, ClassroomId = room.Id,
                Day = d.Day, Start = d.Start!, End = d.End!
            };
            var conflict = FindConflict(all.Concat(added), candidate);
            if (conflict is not null) return (null, conflict);
            added.Add(candidate);
        }
        return (new Prepared(faculty, subject, room, added), "");
    }

    private async Task<OperationResult> SavePreparedAsync(Prepared p)
    {
        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        all.AddRange(p.Candidates);
        await _storage.SaveAsync(JsonStorageService.SchedulesFile, all);
        var summary = string.Join(", ", p.Candidates.Select(a => $"{a.Day.ToString()[..3]} {TimeFormat.Range(a.Start, a.End)}"));
        await _audit.LogAsync("Schedule encoded", $"{p.Faculty.FullName}: {p.Subject.Code} {summary} in {ScheduleRules.RoomLabel(p.Room)}");
        return new OperationResult(true, $"Saved {p.Subject.Code} for {p.Faculty.FullName} ({p.Candidates.Count} day{(p.Candidates.Count == 1 ? "" : "s")}).");
    }

    /// <summary>
    /// Saves the classes of an approved room-use request (called by ChangeRequestService after the owner's Dean said yes). No IMS check here:
    /// the Dean's approval is the permission. The clash checks run again, because the schedule may have changed while the request waited.
    /// </summary>
    public async Task<OperationResult> SaveApprovedRoomUseAsync(RoomUsePlanData plan)
    {
        var (p, error) = await PrepareAsync(plan.FacultyUserId, plan.SubjectCode, plan.ClassroomId,
            plan.Days.Select(d => (d.Day, (string?)d.Start, (string?)d.End)).ToList());
        if (p is null) return new OperationResult(false, error);
        var saved = await SavePreparedAsync(p);
        var message = saved.Message;
        if (plan.FirstReadsId.Length > 0 || plan.EndReadsId.Length > 0)
        {
            var reads = await _scholars.AssignChecksAsync(p.Candidates, plan.FirstReadsId, plan.EndReadsId, dryRun: false);
            message += reads.Success ? (reads.Message.Length > 0 ? " " + reads.Message : "")
                                     : " The READS was NOT assigned: " + reads.Message + " Assign one on the Assign tab.";
        }
        return new OperationResult(true, message);
    }

    /// <summary>
    /// Several class days for one subject at once (for example Monday, Wednesday, Friday). Saved all together or not at all.
    /// Optionally also gives the checks of the class to READS (first check, end check) who are free then, from their Work Time.
    /// A room on a floor owned by ANOTHER school is not booked directly: the request goes to the Dean of the owning school and the classes
    /// are saved only when that Dean approves.
    /// </summary>
    public async Task<OperationResult> AddManyAsync(string facultyUserId, string subjectCode, string classroomId,
        IReadOnlyList<(DayOfWeek Day, string? Start, string? End)> days, string? firstReadsId = null, string? endReadsId = null)
    {
        if (!IsIms) return Fail("Only IMS Personnel can encode schedules.");
        var (p, error) = await PrepareAsync(facultyUserId, subjectCode, classroomId, days);
        if (p is null) return Fail(error);

        var hasReads = !string.IsNullOrEmpty(firstReadsId) || !string.IsNullOrEmpty(endReadsId);
        if (hasReads)
        {
            // Check the READS first, so a class is never saved with a half-done assignment.
            var check = await _scholars.AssignChecksAsync(p.Candidates, firstReadsId, endReadsId, dryRun: true);
            if (!check.Success) return Fail(check.Message);
        }

        if (CampusLayout.NeedsPermission(p.Faculty.School, p.Room))
            return await RequestRoomUseAsync(p, days, firstReadsId, endReadsId);

        var saved = await SavePreparedAsync(p);
        if (!hasReads) return saved;
        var reads = await _scholars.AssignChecksAsync(p.Candidates, firstReadsId, endReadsId, dryRun: false);
        return new OperationResult(true, saved.Message + (reads.Message.Length > 0 ? " " + reads.Message : ""));
    }

    /// <summary>The room is on a floor owned by another school: ask that school's Dean instead of booking it.</summary>
    private async Task<OperationResult> RequestRoomUseAsync(Prepared p, IReadOnlyList<(DayOfWeek Day, string? Start, string? End)> days,
        string? firstReadsId, string? endReadsId)
    {
        var owner = CampusLayout.OwnerOf(p.Room)!;
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        if (!users.Any(u => u.IsUsable && u.Role == UserRole.Dean && u.School == owner))
            return Fail($"{ScheduleRules.RoomLabel(p.Room)} is on a floor of the {owner}, so its Dean must give permission, but that school has no Dean in the app yet. Choose another room.");

        var requests = await _storage.LoadAsync<ChangeRequest>(JsonStorageService.RequestsFile);
        var me = _session.CurrentUser!;
        var plan = new RoomUsePlanData
        {
            FacultyUserId = p.Faculty.Id, SubjectCode = p.Subject.Code, ClassroomId = p.Room.Id,
            Days = days.Select(d => new RoomUseDay(d.Day, d.Start!, d.End!)).ToList(),
            FirstReadsId = firstReadsId ?? "", EndReadsId = endReadsId ?? ""
        };
        var when = string.Join(", ", p.Candidates.Select(a => $"{a.Day.ToString()[..3]} {TimeFormat.Range(a.Start, a.End)}"));
        var forWho = p.Faculty.School.Length > 0 ? AcademicCatalog.ShortName(p.Faculty.School) : "";
        var label = $"{p.Subject.Code} - {p.Subject.Title}";
        var request = new ChangeRequest
        {
            Id = ScheduleRules.NewId("C"), Kind = ChangeKind.RoomUse, ScheduleLabel = label,
            RequesterUserId = me.Id, RequesterName = $"{me.FullName} (IMS Personnel) for {p.Faculty.FullName}",
            ApproverRole = UserRole.Dean, ApproverSchool = owner,
            Summary = $"{forWho} class {p.Subject.Code}: {when} in {ScheduleRules.RoomLabel(p.Room)} ({CampusLayout.OwnerNote(p.Room)})",
            Reason = $"A class of {(forWho.Length > 0 ? forWho : p.Faculty.Department)} ({p.Faculty.FullName}) needs a room on your floor.",
            RoomUsePlan = JsonSerializer.Serialize(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Status = ChangeStatus.Pending, CreatedUtc = DateTime.UtcNow
        };
        requests.Add(request);
        await _storage.SaveAsync(JsonStorageService.RequestsFile, requests);
        await _audit.LogAsync("Room permission requested", $"{request.Summary} (asked the Dean of the {owner})");
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientRole = UserRole.Dean, RecipientSchool = owner, Category = NotificationService.ForApprover, RequestId = request.Id,
            Title = $"Room permission request: {label}",
            Message = $"{request.Summary}\n{request.Reason}\nThe class is saved only if you approve."
        });
        return new OperationResult(true, $"{ScheduleRules.RoomLabel(p.Room)} belongs to the {owner}. The class was NOT saved yet: the request went to the Dean of the {owner}, and it is saved automatically once the Dean approves.");
    }

    public async Task<OperationResult> RemoveAsync(string scheduleId)
    {
        if (!IsIms) return Fail("Only IMS Personnel can remove schedules.");
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = all.FirstOrDefault(x => x.Id == scheduleId);
        if (s is null) return Fail("That schedule was not found.");
        all.Remove(s);
        await _storage.SaveAsync(JsonStorageService.SchedulesFile, all);
        await _audit.LogAsync("Schedule removed", $"{s.FacultyName}: {ScheduleRules.ScheduleLabel(s)}");
        return new OperationResult(true, $"Removed {s.SubjectCode} ({s.FacultyName}).");
    }

    /// <summary>Removes several schedules at once (same subject, same day, same time, or the ones IMS picked). All or nothing.</summary>
    public async Task<OperationResult> RemoveManyAsync(IReadOnlyCollection<string> scheduleIds)
    {
        if (!IsIms) return Fail("Only IMS Personnel can remove schedules.");
        if (scheduleIds.Count == 0) return Fail("Nothing was chosen to remove.");
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var ids = scheduleIds.ToHashSet();
        var gone = all.Where(x => ids.Contains(x.Id)).ToList();
        if (gone.Count == 0) return Fail("Those schedules were not found.");
        all.RemoveAll(x => ids.Contains(x.Id));
        await _storage.SaveAsync(JsonStorageService.SchedulesFile, all);
        await _audit.LogAsync("Schedules removed", string.Join("; ", gone.Select(s => $"{s.FacultyName}: {ScheduleRules.ScheduleLabel(s)}")));
        return new OperationResult(true, gone.Count == 1
            ? $"Removed {gone[0].SubjectCode} ({gone[0].FacultyName})."
            : $"Removed {gone.Count} schedules.");
    }

}
