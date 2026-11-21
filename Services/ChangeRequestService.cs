using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Faculty ask for a schedule change (approved by their department's Chairperson) or a room change (approved by the Dean of the
/// requesting Faculty's school; rooms belong to no school). Once approved, IMS Personnel check the room availability and either
/// approve and apply it to the master schedule or decline it. A room change therefore needs both the Dean and IMS Personnel.
/// </summary>
public sealed class ChangeRequestService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;

    public ChangeRequestService(JsonStorageService storage, SessionService session, AuditService audit, NotificationService notifications)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
        _notifications = notifications;
    }

    /// <summary>
    /// Which checkers (READS Scholars) are touched by a change, so IMS can adjust the checker assignments.
    /// A checker is assigned to a whole school and works in their own vacant times, so a checker is touched when a vacant time
    /// of a Scholar assigned to the class's school overlaps the class's old or new day and time.
    /// </summary>
    private async Task<string> CheckerImpactAsync(ClassSchedule before, ClassSchedule after)
    {
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var vacancies = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var scholars = users.Where(u => u.IsUsable && u.Role == UserRole.ReadsScholar && u.AssignedSchool == before.School).ToList();

        string Covering(ClassSchedule c)
        {
            var names = scholars
                .Where(u => vacancies.Any(v => v.ReadsUserId == u.Id && v.Day == c.Day && ScheduleRules.Overlaps(v.Start, v.End, c.Start, c.End)))
                .Select(u => u.FullName).OrderBy(n => n).ToList();
            return string.Join(", ", names);
        }

        var oldNames = Covering(before);
        var sameSlot = before.Day == after.Day && before.Start == after.Start && before.End == after.End;
        if (sameSlot)
        {
            return oldNames.Length > 0
                ? $"Checkers monitoring this class: {oldNames}. Tell them about the change."
                : "No checker of this school is free at that time. Check the checker assignments.";
        }

        var newNames = Covering(after);
        var text = oldNames.Length > 0
            ? $"Checkers who covered the old time ({before.Day} {ScheduleRules.Range(before.Start, before.End)}): {oldNames}."
            : $"No checker was covering the old time ({before.Day} {ScheduleRules.Range(before.Start, before.End)}).";
        text += newNames.Length > 0
            ? $" Checkers free at the new time ({after.Day} {ScheduleRules.Range(after.Start, after.End)}): {newNames}."
            : $" No checker of this school is free at the new time ({after.Day} {ScheduleRules.Range(after.Start, after.End)}); assign one.";
        return text;
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
            if (!CampusLayout.IsRoomAllowed(s.School, room))
                return Fail($"Classes of the {s.School} are held in building {string.Join(", ", CampusLayout.BuildingsOf(s.School)!)}. Choose a room there.");

            request = new ChangeRequest
            {
                Kind = kind, ProposedClassroomId = room.Id, ApproverRole = UserRole.Dean,
                ApproverSchool = s.School,   // rooms belong to no school: the requesting Faculty's own Dean approves
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

        // Bell: tell the Chairperson (schedule change) or the Dean (room change) that a request is waiting.
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientRole = kind == ChangeKind.Schedule ? UserRole.Chairperson : UserRole.Dean,
            RecipientSchool = s.School,
            RecipientDepartment = kind == ChangeKind.Schedule ? s.Department : "",
            Category = NotificationService.ForApprover, RequestId = request.Id,
            Title = $"{(kind == ChangeKind.Room ? "Room" : "Schedule")} change request from {me.FullName}",
            Message = $"{request.ScheduleLabel}\n{request.Summary}\nReason: {reason}"
        });

        var who = kind == ChangeKind.Schedule ? "your Chairperson" : "the Dean";
        return new OperationResult(true, $"Request sent to {who} for approval. IMS Personnel then check the availability and give the final approval.");
    }

    // ------------------------------------------------------ Chairperson / Dean

    /// <summary>Chairperson: schedule requests of the department. Dean: room requests of the Faculty of the school.</summary>
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

        await _notifications.MarkRequestHandledAsync(requestId, NotificationService.ForApprover);
        var approverTitle = all[i].ApproverRole == UserRole.Dean ? "Dean" : "Chairperson";
        if (approve) await NotifyImsAsync(all[i], me);
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientUserId = all[i].RequesterUserId, Category = NotificationService.ForRequester, RequestId = requestId,
            Title = approve ? $"Your request was approved by the {approverTitle}" : $"Your request was declined by the {approverTitle}",
            Message = $"{all[i].ScheduleLabel}\n{all[i].Summary}" + (approve ? "\nIMS Personnel will check the availability and apply it." : "")
        });

        return new OperationResult(true, approve
            ? "Approved. IMS Personnel have been notified and will check the availability and give the final approval."
            : "Declined. The Faculty member will see the decision.");
    }

    /// <summary>Tell IMS Personnel that a Chairperson or Dean approved a request, with the checkers they may need to adjust.</summary>
    private async Task NotifyImsAsync(ChangeRequest request, UserAccount approver)
    {
        var impact = "";
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = schedules.FirstOrDefault(x => x.Id == request.ScheduleId);
        if (s is not null)
        {
            var subjects = await _storage.LoadAsync<Subject>(JsonStorageService.SubjectsFile);
            impact = await CheckerImpactAsync(s, Apply(s, request, subjects));
        }
        var by = request.ApproverRole == UserRole.Dean ? "Dean" : "Chairperson";
        var kind = request.Kind == ChangeKind.Room ? "Room change" : "Schedule change";
        var message = $"{request.Summary}\nApproved by {by} {approver.FullName}. Check the availability, apply it to the master schedule, and adjust the checkers.";
        if (impact.Length > 0) message += "\n" + impact;
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientRole = UserRole.ImsPersonnel, Category = NotificationService.ForIms, RequestId = request.Id,
            Title = $"{kind} approved: {request.RequesterName}, {request.ScheduleLabel}", Message = message
        });
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

    /// <summary>IMS Personnel: what the availability check says about an approved request (room free, no Faculty clash), shown before they decide.</summary>
    public async Task<string> GetAvailabilityNoteAsync(ChangeRequest request)
    {
        if (_session.Role != UserRole.ImsPersonnel) return "";
        await _storage.InitializeAsync();
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = schedules.FirstOrDefault(x => x.Id == request.ScheduleId);
        if (s is null) return "Availability: the schedule no longer exists.";
        var subjects = await _storage.LoadAsync<Subject>(JsonStorageService.SubjectsFile);
        var conflict = ScheduleService.FindConflict(schedules, Apply(s, request, subjects));
        return conflict is null
            ? (request.Kind == ChangeKind.Room ? "Availability: the room is vacant at that time." : "Availability: no clash with another class.")
            : "Availability: NOT available. " + conflict;
    }

    /// <summary>IMS Personnel: decline an approved request with a reason, for example when the room is not available. Nothing in the schedule changes; the Faculty member sees the reason.</summary>
    public async Task<OperationResult> DeclineAsync(string requestId, string reason)
    {
        var me = _session.CurrentUser;
        if (me is null || me.Role != UserRole.ImsPersonnel) return Fail("Only IMS Personnel can decline here.");
        reason = reason.Trim();
        if (reason.Length == 0) return Fail("Give a reason for declining.");

        var requests = await LoadAsync();
        var ri = requests.FindIndex(r => r.Id == requestId && r.Status == ChangeStatus.Approved);
        if (ri < 0) return Fail("That request is not waiting for IMS Personnel.");

        var request = requests[ri];
        requests[ri] = request with { Status = ChangeStatus.Declined, DecidedUtc = DateTime.UtcNow, DecidedByName = $"{me.FullName} (IMS Personnel)", DeclineReason = reason };
        await _storage.SaveAsync(JsonStorageService.RequestsFile, requests);
        await _audit.LogAsync("Change declined by IMS", $"{request.RequesterName}: {request.ScheduleLabel}. {request.Summary}. Reason: {reason}");
        await _notifications.MarkRequestHandledAsync(request.Id, NotificationService.ForIms);
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientUserId = request.RequesterUserId, Category = NotificationService.ForRequester, RequestId = request.Id,
            Title = "Your request was declined by IMS Personnel",
            Message = $"{request.ScheduleLabel}\n{request.Summary}\nReason: {reason}"
        });
        return new OperationResult(true, "Declined. The schedule was not changed; the Faculty member will see your reason.");
    }

    /// <summary>IMS Personnel: approve an approved request. The requested room (or schedule change) is set in the master schedule automatically.</summary>
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

        await _notifications.MarkRequestHandledAsync(request.Id, NotificationService.ForIms);
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientUserId = request.RequesterUserId, Category = NotificationService.ForRequester, RequestId = request.Id,
            Title = "Your change was applied",
            Message = $"{request.ScheduleLabel}\n{request.Summary}\nIt is now in the master schedule."
        });

        // Checkers (READS Scholars) of this school whose vacant times touch the class before or after the change.
        var message = $"Applied: {before.SubjectCode} ({before.FacultyName}) updated.";
        var impact = await CheckerImpactAsync(before, after);
        if (impact.Length > 0) message += " Adjust the checker assignments: " + impact;
        return new OperationResult(true, message);
    }
}
