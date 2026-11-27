using System.Text.Json;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Faculty ask for a schedule change (approved by their department's Chairperson) or a room change. Who approves a room change depends on
/// who owns the floor of the new room (CampusLayout): a floor of the Faculty's own school or an unowned floor, the Faculty's own Dean
/// (CRE has no Dean: its Chairperson); a floor owned by ANOTHER school, the Dean of that owning school. Once approved, IMS Personnel check the
/// room availability and either approve and apply it to the master schedule or decline it.
/// A third kind, RoomUse, is created by the Schedules page when IMS encode a class in a room owned by another school; the owner's Dean decides it
/// and the classes are saved only on approval.
/// </summary>
public sealed class ChangeRequestService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly ScheduleService _schedulesService;

    public ChangeRequestService(JsonStorageService storage, SessionService session, AuditService audit, NotificationService notifications,
        ScheduleService schedulesService)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
        _notifications = notifications;
        _schedulesService = schedulesService;
    }

    /// <summary>
    /// Which checkers (READS Scholars) are touched by a change, so IMS can adjust the checker assignments.
    /// The checkers touched are the ones IMS assigned this class to. When the day or time changes, IMS also sees which Scholars of the
    /// school are free at the new time and whether the assigned checkers still have the class inside their work times.
    /// </summary>
    private async Task<string> CheckerImpactAsync(ClassSchedule before, ClassSchedule after)
    {
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var vacancies = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var assignments = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);
        var scholars = users.Where(u => u.IsUsable && u.Role == UserRole.ReadsScholar && u.AssignedSchool == before.School).ToList();

        var assigned = scholars.Where(u => assignments.Any(a => a.ReadsUserId == u.Id && a.ScheduleId == before.Id))
            .OrderBy(u => u.FullName).ToList();
        var assignedNames = string.Join(", ", assigned.Select(u => u.FullName));

        var sameSlot = before.Day == after.Day && before.Start == after.Start && before.End == after.End;
        if (sameSlot)
        {
            return assigned.Count > 0
                ? $"Checkers assigned to this class: {assignedNames}. They will be told about the change."
                : "No checker is assigned to this class yet. Assign one on the Assign tab.";
        }

        bool Fits(UserAccount u, ClassSchedule c) =>
            vacancies.Any(v => v.ReadsUserId == u.Id && v.Day == c.Day && ScheduleRules.Contains(v.Start, v.End, c.Start, c.End));

        var text = assigned.Count > 0
            ? $"Checkers assigned to this class: {assignedNames}."
            : "No checker was assigned to this class.";
        var lost = assigned.Where(u => !Fits(u, after)).Select(u => u.FullName).ToList();
        if (lost.Count > 0)
            text += $" At the new time ({after.Day} {ScheduleRules.Range(after.Start, after.End)}) it is no longer inside the work times of {string.Join(", ", lost)}; assign it again.";

        var free = scholars.Where(u => Fits(u, after)).Select(u => u.FullName).OrderBy(n => n).ToList();
        text += free.Count > 0
            ? $" Checkers free at the new time: {string.Join(", ", free)}."
            : $" No checker of this school is free at the new time ({after.Day} {ScheduleRules.Range(after.Start, after.End)}); assign one.";
        return text;
    }

    /// <summary>
    /// Bell notice for every checker (READS Scholar) IMS assigned this class to. They are the ones who would walk to the old room or
    /// the old time, so they are told the new room (room change) or the new day and time (schedule change).
    /// Returns how many checkers were notified.
    /// </summary>
    private async Task<int> NotifyCheckersAsync(ChangeRequest request, ClassSchedule before, ClassSchedule after, IReadOnlyList<Classroom> rooms)
    {
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var vacancies = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var assignments = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);
        var scholars = users.Where(u => u.IsUsable && u.Role == UserRole.ReadsScholar
            && assignments.Any(a => a.ReadsUserId == u.Id && a.ScheduleId == before.Id)).ToList();

        var oldRoom = ScheduleRules.RoomLabel(rooms.FirstOrDefault(r => r.Id == before.ClassroomId));
        var newRoom = ScheduleRules.RoomLabel(rooms.FirstOrDefault(r => r.Id == after.ClassroomId));
        var subject = $"{after.SubjectCode} - {after.SubjectTitle} ({after.FacultyName})";

        string title, message;
        if (request.Kind == ChangeKind.Room)
        {
            title = $"Room changed: {after.SubjectCode}, now {newRoom}";
            message = $"{subject}\n{after.Day} {ScheduleRules.Range(after.Start, after.End)}\nMoved from {oldRoom} to {newRoom}. Check the class in the new room. Approved by the Dean and IMS Personnel.";
        }
        else
        {
            title = $"Class schedule changed: {after.SubjectCode}";
            message = $"{subject}\nWas: {before.Day} {ScheduleRules.Range(before.Start, before.End)}, {oldRoom}\nNow: {after.Day} {ScheduleRules.Range(after.Start, after.End)}, {newRoom}\nApproved by the Chairperson and IMS Personnel.";
        }

        var told = 0;
        foreach (var u in scholars)
        {
            var fits = vacancies.Any(v => v.ReadsUserId == u.Id && v.Day == after.Day && ScheduleRules.Overlaps(v.Start, v.End, after.Start, after.End));
            await _notifications.NotifyAsync(new AppNotification
            {
                RecipientUserId = u.Id, Category = NotificationService.ForChecker, RequestId = request.Id,
                Title = title,
                Message = fits ? message : message + "\nThe new time is outside your work times, so IMS Personnel will assign the class again."
            });
            told++;
        }
        return told;
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
            // Who must say yes depends on who owns the floor of the new room.
            var owner = CampusLayout.OwnerOf(room);
            var foreign = owner is not null && !string.Equals(owner, s.School, StringComparison.OrdinalIgnoreCase);
            var approverRole = UserRole.Dean;
            var approverSchool = s.School;
            var approverDepartment = "";
            string note = "";
            if (foreign)
            {
                var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
                if (!users.Any(u => u.IsUsable && u.Role == UserRole.Dean && u.School == owner))
                    return Fail($"{ScheduleRules.RoomLabel(room)} is on a floor of the {owner}. Its Dean must give permission, but that school has no Dean in the app yet. Choose another room.");
                approverSchool = owner!;
                note = $" (floor owned by {AcademicCatalog.ShortName(owner)}: needs permission from its Dean)";
            }
            else if (AcademicCatalog.IsIndependent(s.School))
            {
                // The Center for Religious Education has no Dean: its Chairperson approves.
                approverRole = UserRole.Chairperson;
                approverDepartment = s.Department;
            }

            request = new ChangeRequest
            {
                Kind = kind, ProposedClassroomId = room.Id, ApproverRole = approverRole,
                ApproverSchool = approverSchool, ApproverDepartment = approverDepartment,
                Summary = $"Room: {ScheduleRules.RoomLabel(oldRoom)} to {ScheduleRules.RoomLabel(room)}{note}"
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

        // Bell: tell whoever must approve (the Chairperson, or the Dean of the school that owns the floor) that a request is waiting.
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientRole = request.ApproverRole,
            RecipientSchool = request.ApproverSchool,
            RecipientDepartment = request.ApproverRole == UserRole.Chairperson ? request.ApproverDepartment : "",
            Category = NotificationService.ForApprover, RequestId = request.Id,
            Title = $"{(kind == ChangeKind.Room ? "Room" : "Schedule")} change request from {me.FullName}",
            Message = $"{request.ScheduleLabel}\n{request.Summary}\nReason: {reason}"
        });

        var who = request.ApproverRole == UserRole.Chairperson ? "your Chairperson"
            : request.ApproverSchool != s.School ? $"the Dean of the {request.ApproverSchool}" : "the Dean";
        return new OperationResult(true, $"Request sent to {who} for approval. IMS Personnel then check the availability and give the final approval.");
    }

    // ------------------------------------------------------ Chairperson / Dean

    /// <summary>Chairperson: schedule requests of the department (and room requests of CRE Faculty). Dean: room requests of the school's own Faculty, and permission requests for rooms on the school's floors, from any school.</summary>
    public async Task<List<ChangeRequest>> GetPendingForMeAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return new();
        var all = await LoadAsync();
        var pending = me.Role switch
        {
            UserRole.Chairperson => all.Where(r => (r.Kind == ChangeKind.Schedule || (r.Kind == ChangeKind.Room && r.ApproverRole == UserRole.Chairperson))
                                                   && r.ApproverSchool == me.School && r.ApproverDepartment == me.Department),
            UserRole.Dean => all.Where(r => r.Kind is ChangeKind.Room or ChangeKind.RoomUse && r.ApproverRole == UserRole.Dean && r.ApproverSchool == me.School),
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
        if (all[i].Kind == ChangeKind.RoomUse) return await DecideRoomUseAsync(all, i, me, approve);
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

    /// <summary>The owner's Dean decides a RoomUse request. Approving saves the classes IMS encoded (and gives their READS); declining saves nothing.</summary>
    private async Task<OperationResult> DecideRoomUseAsync(List<ChangeRequest> all, int i, UserAccount me, bool approve)
    {
        var request = all[i];
        var message = "";
        if (approve)
        {
            var plan = JsonSerializer.Deserialize<RoomUsePlanData>(request.RoomUsePlan, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (plan is null) return Fail("This request is damaged. Decline it and ask IMS Personnel to encode the class again.");
            var saved = await _schedulesService.SaveApprovedRoomUseAsync(plan);
            if (!saved.Success) return Fail("Cannot approve yet: " + saved.Message + " Decline the request if it cannot be fixed.");
            message = saved.Message;
        }

        all[i] = request with
        {
            Status = approve ? ChangeStatus.Applied : ChangeStatus.Declined,
            DecidedUtc = DateTime.UtcNow, DecidedByName = me.FullName,
            AppliedUtc = approve ? DateTime.UtcNow : null, AppliedByName = approve ? me.FullName : ""
        };
        await _storage.SaveAsync(JsonStorageService.RequestsFile, all);
        await _audit.LogAsync(approve ? "Room permission granted" : "Room permission declined", $"{request.Summary} (Dean {me.FullName})");
        await _notifications.MarkRequestHandledAsync(request.Id, NotificationService.ForApprover);
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientUserId = request.RequesterUserId, Category = NotificationService.ForRequester, RequestId = request.Id,
            Title = approve ? $"The Dean of the {me.School} allowed the room" : $"The Dean of the {me.School} declined the room",
            Message = $"{request.Summary}\n" + (approve ? message : "The class was not saved. Choose another room on the Schedules page.")
        });
        return new OperationResult(true, approve ? "Permission granted. " + message : "Declined. The class was not saved and IMS Personnel were told.");
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

        // The class may have moved to another floor: keep the checkers' assigned floor in step with the new room.
        var newRoom = rooms.FirstOrDefault(r => r.Id == after.ClassroomId);
        if (newRoom is not null)
        {
            var assigned = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);
            var moved = false;
            for (var k = 0; k < assigned.Count; k++)
                if (assigned[k].ScheduleId == after.Id && (assigned[k].Building != newRoom.Building || assigned[k].Floor != newRoom.Floor))
                { assigned[k] = assigned[k] with { Building = newRoom.Building, Floor = newRoom.Floor }; moved = true; }
            if (moved) await _storage.SaveAsync(JsonStorageService.CheckAssignmentsFile, assigned);
        }

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

        // Checkers (READS Scholars) of this school whose work times touch the class before or after the change get a bell notice.
        var told = await NotifyCheckersAsync(request, before, after, rooms);
        var message = $"Applied: {before.SubjectCode} ({before.FacultyName}) updated."
            + (told > 0 ? $" {told} READS checker{(told == 1 ? " was" : "s were")} notified." : " No READS checker is assigned to this class, so no checker was notified.");
        var impact = await CheckerImpactAsync(before, after);
        if (impact.Length > 0) message += " " + impact;
        return new OperationResult(true, message);
    }
}
