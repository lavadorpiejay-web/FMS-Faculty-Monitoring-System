using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Faculty ask for a schedule change (approved by their department's Chairperson) or a room change (approved by the Dean of the
/// school that owns the room). Once approved, IMS Personnel see it in their Changes list and apply it to the master schedule.
/// </summary>
public sealed class ChangeRequestService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;

    public ChangeRequestService(JsonStorageService storage, SessionService session, AuditService audit)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
    }

    private static OperationResult Fail(string message) => new(false, message);
    private static bool Has(string? v) => !string.IsNullOrEmpty(v) && v != ScheduleRules.NoChange;

    private async Task<List<ChangeRequest>> LoadAsync()
    {
        await _storage.InitializeAsync();
        return await _storage.LoadAsync<ChangeRequest>(JsonStorageService.RequestsFile);
    }

    /// <summary>The schedule with the request's proposed values applied.</summary>
    private static ClassSchedule Apply(ClassSchedule s, ChangeRequest r, IReadOnlyList<Subject> subjects)
    {
        if (r.Kind == ChangeKind.Room) return s with { ClassroomId = r.ProposedClassroomId };
        var c = s;
        if (r.ProposedDay is { } day) c = c with { Day = day };
        if (r.ProposedStart.Length > 0) c = c with { Start = r.ProposedStart };
        if (r.ProposedEnd.Length > 0) c = c with { End = r.ProposedEnd };
        if (r.ProposedSubjectCode.Length > 0)
        {
            var subject = subjects.FirstOrDefault(x => x.Code == r.ProposedSubjectCode);
            c = c with { SubjectCode = r.ProposedSubjectCode, SubjectTitle = subject?.Title ?? c.SubjectTitle };
        }
        return c;
    }

    // ---------------------------------------------------------------- Faculty

    public async Task<List<ChangeRequest>> GetMineAsync()
    {
        var me = _session.CurrentUser;
        if (me is null || me.Role != UserRole.Faculty) return new();
        return (await LoadAsync()).Where(r => r.RequesterUserId == me.Id).OrderByDescending(r => r.CreatedUtc).ToList();
    }

    public async Task<OperationResult> SubmitAsync(ChangeKind kind, string scheduleId, string? newDay, string? newStart,
        string? newEnd, string? newSubjectCode, string? newClassroomId, string reason)
    {
        var me = _session.CurrentUser;
        if (me is null || me.Role != UserRole.Faculty) return Fail("Only Faculty can submit change requests.");
        reason = reason.Trim();
        if (reason.Length == 0) return Fail("Give a short reason for the request.");

        await _storage.InitializeAsync();
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = schedules.FirstOrDefault(x => x.Id == scheduleId && x.FacultyUserId == me.Id);
        if (s is null) return Fail("Choose one of your classes.");

        var requests = await LoadAsync();
        if (requests.Any(r => r.ScheduleId == s.Id && r.Kind == kind && r.Status is ChangeStatus.Pending or ChangeStatus.Approved))
            return Fail("There is already an open request of this kind for that class.");

        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        var subjects = await _storage.LoadAsync<Subject>(JsonStorageService.SubjectsFile);
        var oldRoom = rooms.FirstOrDefault(r => r.Id == s.ClassroomId);

        ChangeRequest request;
        if (kind == ChangeKind.Schedule)
        {
            var subjectCode = Has(newSubjectCode) ? newSubjectCode! : "";
            if (subjectCode.Length > 0 && !subjects.Any(x => x.Code == subjectCode && x.Department == s.Department))
                return Fail("Choose a subject of your department.");

            request = new ChangeRequest
            {
                Kind = kind, ProposedDay = Has(newDay) ? ScheduleRules.ParseDay(newDay!) : null,
                ProposedStart = Has(newStart) ? newStart! : "", ProposedEnd = Has(newEnd) ? newEnd! : "",
                ProposedSubjectCode = subjectCode == s.SubjectCode ? "" : subjectCode,
                ApproverRole = UserRole.Chairperson, ApproverSchool = s.School, ApproverDepartment = s.Department
            };
            var candidate = Apply(s, request, subjects);
            if (candidate == s) return Fail("Choose at least one thing to change.");
            var timeError = ScheduleRules.CheckTimes(candidate.Start, candidate.End);
            if (timeError is not null) return Fail(timeError);
            var conflict = ScheduleService.FindConflict(schedules, candidate);
            if (conflict is not null) return Fail(conflict);

            var parts = new List<string>();
            if (candidate.Day != s.Day) parts.Add($"Day: {s.Day} to {candidate.Day}");
            if (candidate.Start != s.Start || candidate.End != s.End)
                parts.Add($"Time: {ScheduleRules.Range(s.Start, s.End)} to {ScheduleRules.Range(candidate.Start, candidate.End)}");
            if (candidate.SubjectCode != s.SubjectCode) parts.Add($"Subject: {s.SubjectCode} to {candidate.SubjectCode}");
            request = request with { Summary = string.Join("; ", parts) };
        }
        else
        {
            var room = rooms.FirstOrDefault(r => r.Id == newClassroomId);
            if (room is null) return Fail("Choose the new classroom.");
            if (room.Id == s.ClassroomId) return Fail("That is already the class's room.");

            request = new ChangeRequest
            {
                Kind = kind, ProposedClassroomId = room.Id, ApproverRole = UserRole.Dean,
                ApproverSchool = room.School.Length > 0 ? room.School : s.School,
                Summary = $"Room: {ScheduleRules.RoomLabel(oldRoom)} to {ScheduleRules.RoomLabel(room)}"
            };
            var conflict = ScheduleService.FindConflict(schedules, Apply(s, request, subjects));
            if (conflict is not null) return Fail(conflict);
        }

        request = request with
        {
            Id = ScheduleRules.NewId("C"), ScheduleId = s.Id, ScheduleLabel = ScheduleRules.ScheduleLabel(s),
            RequesterUserId = me.Id, RequesterName = me.FullName, Reason = reason,
            Status = ChangeStatus.Pending, CreatedUtc = DateTime.UtcNow
        };
        requests.Add(request);
        await _storage.SaveAsync(JsonStorageService.RequestsFile, requests);
        await _audit.LogAsync("Change requested", $"{me.FullName}: {request.ScheduleLabel}. {request.Summary}");

        var who = kind == ChangeKind.Schedule ? "your Chairperson" : "the Dean";
        return new OperationResult(true, $"Request sent to {who} for approval.");
    }

    // ------------------------------------------------------ Chairperson / Dean

    /// <summary>Chairperson: schedule requests of the department. Dean: room requests for rooms of the school.</summary>
    public async Task<List<ChangeRequest>> GetPendingForMeAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return new();
        var all = await LoadAsync();
        var pending = me.Role switch
        {
            UserRole.Chairperson => all.Where(r => r.Kind == ChangeKind.Schedule && r.ApproverSchool == me.School && r.ApproverDepartment == me.Department),
            UserRole.Dean => all.Where(r => r.Kind == ChangeKind.Room && r.ApproverSchool == me.School),
            _ => Enumerable.Empty<ChangeRequest>()
        };
        return pending.Where(r => r.Status == ChangeStatus.Pending).OrderBy(r => r.CreatedUtc).ToList();
    }

    public async Task<OperationResult> DecideAsync(string requestId, bool approve)
    {
        var me = _session.CurrentUser;
        if (me is null) return Fail("Sign in again.");
        var allowed = await GetPendingForMeAsync();
        if (!allowed.Any(r => r.Id == requestId)) return Fail("That request is not waiting for you.");

        var all = await LoadAsync();
        var i = all.FindIndex(r => r.Id == requestId);
        all[i] = all[i] with
        {
            Status = approve ? ChangeStatus.Approved : ChangeStatus.Declined,
            DecidedUtc = DateTime.UtcNow, DecidedByName = me.FullName
        };
        await _storage.SaveAsync(JsonStorageService.RequestsFile, all);
        await _audit.LogAsync(approve ? "Change approved" : "Change declined", $"{all[i].RequesterName}: {all[i].ScheduleLabel}. {all[i].Summary}");

        return new OperationResult(true, approve
            ? "Approved. IMS Personnel have been notified and will update the schedule."
            : "Declined. The Faculty member will see the decision.");
    }

    // ------------------------------------------------------------------- IMS

    /// <summary>The notification list for IMS Personnel: approved requests waiting to be applied.</summary>
    public async Task<List<ChangeRequest>> GetApprovedForImsAsync()
    {
        if (_session.Role != UserRole.ImsPersonnel) return new();
        return (await LoadAsync()).Where(r => r.Status == ChangeStatus.Approved).OrderBy(r => r.DecidedUtc).ToList();
    }

    public async Task<int> CountOpenAsync()
    {
        if (_session.Role != UserRole.ImsPersonnel) return 0;
        return (await LoadAsync()).Count(r => r.Status == ChangeStatus.Pending);
    }

    public async Task<OperationResult> ApplyAsync(string requestId)
    {
        var me = _session.CurrentUser;
        if (me is null || me.Role != UserRole.ImsPersonnel) return Fail("Only IMS Personnel can apply approved changes.");

        var requests = await LoadAsync();
        var ri = requests.FindIndex(r => r.Id == requestId && r.Status == ChangeStatus.Approved);
        if (ri < 0) return Fail("That request is not approved or was already applied.");
        var request = requests[ri];

        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var si = schedules.FindIndex(s => s.Id == request.ScheduleId);
        if (si < 0) return Fail("The schedule no longer exists.");
        var subjects = await _storage.LoadAsync<Subject>(JsonStorageService.SubjectsFile);
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);

        var before = schedules[si];
        var after = Apply(before, request, subjects);
        var conflict = ScheduleService.FindConflict(schedules, after);
        if (conflict is not null) return Fail("Cannot apply yet: " + conflict);

        schedules[si] = after;
        await _storage.SaveAsync(JsonStorageService.SchedulesFile, schedules);
        requests[ri] = request with { Status = ChangeStatus.Applied, AppliedUtc = DateTime.UtcNow, AppliedByName = me.FullName };
        await _storage.SaveAsync(JsonStorageService.RequestsFile, requests);
        await _audit.LogAsync("Change applied", $"{before.FacultyName}: {ScheduleRules.ScheduleLabel(before)} now {ScheduleRules.ScheduleLabel(after)} in {ScheduleRules.RoomLabel(rooms.FirstOrDefault(r => r.Id == after.ClassroomId))}");

        // Checkers whose duty covered the class before or covers it now may need a new duty or area.
        var duties = await _storage.LoadAsync<DutyAssignment>(JsonStorageService.DutiesFile);
        var oldRoom = rooms.FirstOrDefault(r => r.Id == before.ClassroomId);
        var newRoom = rooms.FirstOrDefault(r => r.Id == after.ClassroomId);
        var affected = duties
            .Where(d => (d.Day == before.Day && ScheduleRules.Overlaps(d.Start, d.End, before.Start, before.End) && ScheduleRules.DutyCovers(d, before, oldRoom))
                     || (d.Day == after.Day && ScheduleRules.Overlaps(d.Start, d.End, after.Start, after.End) && ScheduleRules.DutyCovers(d, after, newRoom)))
            .Select(d => d.ReadsName).Distinct().OrderBy(n => n).ToList();

        var message = $"Applied: {before.SubjectCode} ({before.FacultyName}) updated.";
        if (affected.Count > 0) message += $" Review the duty assignments of {string.Join(", ", affected)} on the Duty tab.";
        return new OperationResult(true, message);
    }
}
