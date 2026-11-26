using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record ScholarInfo(string UserId, string Name, string Label, string AssignedSchool, string OwnSchool,
    int ClassCount = 0, string Floors = "")
{
    public string Assignment
    {
        get
        {
            var text = AssignedSchool.Length == 0 ? "Not assigned" : $"Monitors {AssignedSchool}";
            if (OwnSchool.Length > 0) text += $" (studies in {OwnSchool})";
            if (AssignedSchool.Length > 0)
                text += ClassCount == 0 ? ". No classes to check yet"
                    : $". Checks {ClassCount} class{(ClassCount == 1 ? "" : "es")} on {Floors}";
            return text;
        }
    }
}

public sealed record OperationResult(bool Success, string Message);

/// <summary>The classes IMS can assign on one floor, with a line saying what was left out.</summary>
public sealed record ClassPickResult(List<ClassPick> Items, string Message);

/// <summary>
/// IMS Personnel assign READS Scholars to a school (all of its departments), then give each Scholar the exact checks to do:
/// classes of that school, on a chosen building and floor, whose first check and/or end check fall inside the Scholar's vacant time.
/// When a class runs past the end of a Scholar's vacant time, another Scholar takes over for the end check.
/// A Scholar is never assigned to the school they study in. Only IMS Personnel may use it.
/// </summary>
public sealed class ScholarAssignmentService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;

    public ScholarAssignmentService(JsonStorageService storage, SessionService session, NotificationService notifications, AuditService audit)
    {
        _storage = storage;
        _session = session;
        _notifications = notifications;
        _audit = audit;
    }

    private bool IsIms => _session.Role == UserRole.ImsPersonnel;

    private static string FloorText(string building, string floor) => $"{building} floor {floor}";

    public async Task<List<ScholarInfo>> GetScholarsAsync()
    {
        if (!IsIms) return new();
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var assigned = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);
        var live = schedules.Select(s => s.Id).ToHashSet();

        return users
            .Where(u => u.Role == UserRole.ReadsScholar && u.IsUsable)
            .OrderBy(u => u.FullName)
            .Select(u =>
            {
                var mine = assigned.Where(a => a.ReadsUserId == u.Id && live.Contains(a.ScheduleId)).ToList();
                var floors = string.Join(", ", mine.Select(a => FloorText(a.Building, a.Floor)).Distinct().OrderBy(f => f));
                return new ScholarInfo(u.Id, u.FullName, $"{u.FullName} ({u.SisId})", u.AssignedSchool, u.OwnSchool, mine.Count, floors);
            })
            .ToList();
    }

    public Task<OperationResult> AssignAsync(string userId, string school)
    {
        if (!AcademicCatalog.IsValidSchool(school))
            return Task.FromResult(new OperationResult(false, "Choose a school."));
        return UpdateAsync(userId, school);
    }

    public Task<OperationResult> ClearAsync(string userId) => UpdateAsync(userId, "");

    private async Task<OperationResult> UpdateAsync(string userId, string school)
    {
        if (!IsIms) return new OperationResult(false, "Only IMS Personnel can assign READS.");

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var i = users.FindIndex(u => u.Id == userId && u.Role == UserRole.ReadsScholar);
        if (i < 0) return new OperationResult(false, "That READS was not found.");

        var name = users[i].FullName;
        if (school.Length > 0 && string.Equals(users[i].OwnSchool, school, StringComparison.OrdinalIgnoreCase))
            return new OperationResult(false, $"{name} studies in the {school}. Assign a different school, so they do not check the faculty of their own school.");

        var schoolChanged = !string.Equals(users[i].AssignedSchool, school, StringComparison.OrdinalIgnoreCase);
        users[i] = users[i] with { AssignedSchool = school, AssignedDepartment = "" };
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        // The classes IMS gave this Scholar belong to the old school, so they are dropped when the school changes or is removed.
        var droppedClasses = 0;
        if (schoolChanged)
        {
            var all = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);
            droppedClasses = all.RemoveAll(a => a.ReadsUserId == userId);
            if (droppedClasses > 0) await _storage.SaveAsync(JsonStorageService.CheckAssignmentsFile, all);
        }

        // Bell: tell the Scholar about the new assignment.
        await _notifications.NotifyAsync(new AppNotification
        {
            RecipientUserId = userId, Category = NotificationService.ForAssignment,
            Title = school.Length == 0 ? "Your school assignment was removed" : $"You were assigned to monitor the {school}",
            Message = school.Length == 0
                ? "IMS Personnel removed your school assignment. You will not check any classes until you are assigned again."
                : $"IMS Personnel assigned you to the {school}. IMS will give you the classes and floors to check inside your vacant times."
        });

        var extra = droppedClasses > 0 ? $" Their {droppedClasses} assigned class{(droppedClasses == 1 ? "" : "es")} were removed, so assign the classes again." : "";
        return new OperationResult(true, school.Length == 0
            ? $"{name} is no longer assigned to a school.{extra}"
            : $"{name} assigned to {school}.{extra}");
    }

    // ------------------------------------------------ classes and floors

    /// <summary>The Scholar's vacant times as text (for example "Mon 7:30 AM - 12:00 PM"), so IMS can see what the classes must fit in.</summary>
    public async Task<string> GetVacantTextAsync(string userId)
    {
        if (!IsIms) return "";
        await _storage.InitializeAsync();
        var vacancies = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var mine = vacancies.Where(v => v.ReadsUserId == userId)
            .OrderBy(v => v.Day == DayOfWeek.Sunday ? 7 : (int)v.Day).ThenBy(v => v.Start).ToList();
        return mine.Count == 0
            ? "This READS has not entered vacant times yet (Duty tab), so no class can be assigned."
            : "Vacant times: " + string.Join("; ", mine.Select(v => $"{v.Day.ToString()[..3]} {TimeFormat.Range(v.Start, v.End)}"));
    }

    /// <summary>Minute of day when the first check opens (5 minutes after the start) or the end check opens (15 minutes before the end).</summary>
    private static int CheckMoment(ClassSchedule s, bool first) =>
        first ? ScheduleRules.Minutes(s.Start) + AttendanceService.CheckMinutes : ScheduleRules.Minutes(s.End) - AttendanceService.EndWindowMinutes;

    /// <summary>True when the Scholar is free (inside a vacant time) at that moment of the class's day.</summary>
    private static bool Free(IEnumerable<VacantSlot> vacancies, ClassSchedule s, bool first)
    {
        var m = CheckMoment(s, first);
        return vacancies.Any(v => v.Day == s.Day && ScheduleRules.Minutes(v.Start) <= m && m < ScheduleRules.Minutes(v.End));
    }

    /// <summary>
    /// Classes IMS can give this Scholar on one floor. A class is listed when at least one of its two checks (5 minutes after the start,
    /// 15 minutes before the end) falls inside one of the Scholar's vacant times. A class that runs past the end of the vacant time is split:
    /// this Scholar takes the first check and another Scholar, whose vacant time starts there, takes over for the end check (or the other way round).
    /// A check another Scholar already holds cannot be ticked.
    /// </summary>
    public async Task<ClassPickResult> GetClassesAsync(string userId, string building, string floor)
    {
        if (!IsIms) return new(new(), "");
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var me = users.FirstOrDefault(u => u.Id == userId && u.Role == UserRole.ReadsScholar);
        if (me is null) return new(new(), "Choose a READS.");
        if (me.AssignedSchool.Length == 0) return new(new(), "Assign this READS to a school first (above), then choose their classes.");

        var rooms = (await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile))
            .Where(r => r.Building == building && r.Floor == floor).ToDictionary(r => r.Id);
        var schedules = (await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile))
            .Where(s => s.School == me.AssignedSchool && rooms.ContainsKey(s.ClassroomId)).ToList();
        var vacancies = (await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile)).Where(v => v.ReadsUserId == userId).ToList();
        var assigned = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);

        string NameOf(string id) => users.FirstOrDefault(u => u.Id == id)?.FullName ?? "another READS";

        var items = new List<ClassPick>();
        var outside = 0;
        foreach (var s in schedules.OrderBy(s => s.Day == DayOfWeek.Sunday ? 7 : (int)s.Day).ThenBy(s => s.Start).ThenBy(s => rooms[s.ClassroomId].RoomNo))
        {
            var mine = assigned.FirstOrDefault(a => a.ScheduleId == s.Id && a.ReadsUserId == userId);
            var otherStart = assigned.FirstOrDefault(a => a.ScheduleId == s.Id && a.ReadsUserId != userId && a.DoesStart);
            var otherEnd = assigned.FirstOrDefault(a => a.ScheduleId == s.Id && a.ReadsUserId != userId && a.DoesEnd);

            var freeStart = Free(vacancies, s, true);
            var freeEnd = Free(vacancies, s, false);
            // A check the Scholar already holds stays tickable even if their vacant times changed, so IMS can still take it off.
            var canStart = otherStart is null && (freeStart || (mine?.DoesStart ?? false));
            var canEnd = otherEnd is null && (freeEnd || (mine?.DoesEnd ?? false));
            if (!freeStart && !freeEnd && mine is null) { outside++; continue; }

            var notes = new List<string>();
            if (otherStart is not null) notes.Add($"First check: {NameOf(otherStart.ReadsUserId)}");
            if (otherEnd is not null) notes.Add($"End check: {NameOf(otherEnd.ReadsUserId)}");
            if (otherStart is null && !freeStart && !(mine?.DoesStart ?? false)) notes.Add($"First check ({TimeFormat.To12h(ScheduleRules.HHmmOf(CheckMoment(s, true)))}) is outside this READS' vacant time");
            if (otherEnd is null && !freeEnd && !(mine?.DoesEnd ?? false)) notes.Add($"End check ({TimeFormat.To12h(ScheduleRules.HHmmOf(CheckMoment(s, false)))}) is outside this READS' vacant time: another READS must take over");
            if (mine is not null && ((mine.DoesStart && !freeStart) || (mine.DoesEnd && !freeEnd))) notes.Add("No longer inside this READS' vacant time. Untick it to remove it");

            var room = rooms[s.ClassroomId];
            items.Add(new ClassPick(s.Id,
                $"{s.Day} {TimeFormat.Range(s.Start, s.End)} | Room {room.RoomNo}",
                $"{s.SubjectCode} - {s.SubjectTitle} | {s.FacultyName}",
                string.Join(". ", notes), canStart, canEnd, mine?.DoesStart ?? false, mine?.DoesEnd ?? false));
        }

        var message = items.Count == 0
            ? $"No class of the {me.AssignedSchool} on {FloorText(building, floor)} has a check inside this READS' vacant times."
            : "";
        if (outside > 0)
            message += $"{(message.Length > 0 ? " " : "")}{outside} other class{(outside == 1 ? "" : "es")} on this floor {(outside == 1 ? "has" : "have")} no check inside the vacant times and {(outside == 1 ? "is" : "are")} not listed.";
        return new ClassPickResult(items, message);
    }

    /// <summary>
    /// Saves the checks ticked for one floor. Classes of that floor that are no longer ticked are unassigned. All or nothing.
    /// Each pick is (class, first check, end check). Classes of one floor may end together; end checks on different floors may not.
    /// </summary>
    public async Task<OperationResult> SaveClassesAsync(string userId, string building, string floor, IReadOnlyList<(string ScheduleId, bool First, bool End)> picks)
    {
        if (!IsIms) return new OperationResult(false, "Only IMS Personnel can assign classes to READS.");
        await _storage.InitializeAsync();

        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var me = users.FirstOrDefault(u => u.Id == userId && u.Role == UserRole.ReadsScholar);
        if (me is null) return new OperationResult(false, "That READS was not found.");
        if (me.AssignedSchool.Length == 0) return new OperationResult(false, "Assign this READS to a school first.");

        var rooms = (await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile)).ToDictionary(r => r.Id);
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var vacancies = (await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile)).Where(v => v.ReadsUserId == userId).ToList();
        var all = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);

        var chosen = new List<(ClassSchedule S, string Part)>();
        foreach (var (id, first, end) in picks.Where(p => p.First || p.End))
        {
            var s = schedules.FirstOrDefault(x => x.Id == id);
            if (s is null || !rooms.TryGetValue(s.ClassroomId, out var room) || room.Building != building || room.Floor != floor)
                return new OperationResult(false, "One of the chosen classes is not on that floor any more. Reload the list.");
            if (s.School != me.AssignedSchool)
                return new OperationResult(false, $"{s.SubjectCode} is not a class of the {me.AssignedSchool}.");

            var mineNow = all.FirstOrDefault(a => a.ScheduleId == s.Id && a.ReadsUserId == userId);
            var label = $"{s.SubjectCode} ({s.Day} {TimeFormat.Range(s.Start, s.End)})";
            if (first)
            {
                if (!(mineNow?.DoesStart ?? false) && !Free(vacancies, s, true))
                    return new OperationResult(false, $"The first check of {label} is not inside this READS' vacant times.");
                if (all.Any(a => a.ScheduleId == s.Id && a.ReadsUserId != userId && a.DoesStart))
                    return new OperationResult(false, $"The first check of {label} is already held by another READS.");
            }
            if (end)
            {
                if (!(mineNow?.DoesEnd ?? false) && !Free(vacancies, s, false))
                    return new OperationResult(false, $"The end check of {label} is not inside this READS' vacant times.");
                if (all.Any(a => a.ScheduleId == s.Id && a.ReadsUserId != userId && a.DoesEnd))
                    return new OperationResult(false, $"The end check of {label} is already held by another READS.");
            }
            chosen.Add((s, first && end ? CheckPart.Both : first ? CheckPart.Start : CheckPart.End));
        }

        // Rooms of the same floor may end together: the Scholar walks the floor during the 15-minute end window, so that is allowed.
        // End checks on two different floors that open less than 15 minutes apart cannot both be done by one Scholar.
        var moments = new List<(DayOfWeek Day, int Minute, string Floor, string Label)>();
        void Add(ClassSchedule s, string part, string floorKey)
        {
            if (part != CheckPart.Start) moments.Add((s.Day, CheckMoment(s, false), floorKey, s.SubjectCode));
        }
        foreach (var (s, part) in chosen) Add(s, part, $"{building}|{floor}");
        foreach (var a in all.Where(a => a.ReadsUserId == userId && !(a.Building == building && a.Floor == floor)))
        {
            var other = schedules.FirstOrDefault(x => x.Id == a.ScheduleId);
            if (other is not null) Add(other, a.Part, $"{a.Building}|{a.Floor}");
        }
        for (var i = 0; i < moments.Count; i++)
            for (var j = i + 1; j < moments.Count; j++)
                if (moments[i].Day == moments[j].Day && moments[i].Floor != moments[j].Floor
                    && Math.Abs(moments[i].Minute - moments[j].Minute) < AttendanceService.EndWindowMinutes)
                    return new OperationResult(false, $"The end checks of {moments[i].Label} and {moments[j].Label} are on different floors at almost the same time on {moments[i].Day}. One READS cannot do both; give one of them to another READS.");

        var before = all.Count(a => a.ReadsUserId == userId && a.Building == building && a.Floor == floor);
        all.RemoveAll(a => a.ReadsUserId == userId && a.Building == building && a.Floor == floor);
        foreach (var (s, part) in chosen)
            all.Add(new CheckAssignment { Id = ScheduleRules.NewId("CA"), ReadsUserId = userId, ScheduleId = s.Id, Building = building, Floor = floor, Part = part });
        await _storage.SaveAsync(JsonStorageService.CheckAssignmentsFile, all);

        await _audit.LogAsync("Classes assigned to READS",
            $"{me.FullName}: {chosen.Count} class{(chosen.Count == 1 ? "" : "es")} on {FloorText(building, floor)}");

        string PartText(string part) => part == CheckPart.Start ? " (first check only)" : part == CheckPart.End ? " (end check only)" : "";
        if (chosen.Count > 0 || before > 0)
            await _notifications.NotifyAsync(new AppNotification
            {
                RecipientUserId = userId, Category = NotificationService.ForAssignment,
                Title = chosen.Count == 0 ? $"Your classes on {FloorText(building, floor)} were removed"
                                          : $"You check {chosen.Count} class{(chosen.Count == 1 ? "" : "es")} on {FloorText(building, floor)}",
                Message = chosen.Count == 0
                    ? "IMS Personnel took the classes on this floor off your list."
                    : "IMS Personnel assigned these classes to you:\n"
                      + string.Join("\n", chosen.OrderBy(c => c.S.Day == DayOfWeek.Sunday ? 7 : (int)c.S.Day).ThenBy(c => c.S.Start)
                          .Select(c => $"{c.S.Day.ToString()[..3]} {TimeFormat.Range(c.S.Start, c.S.End)}, Room {rooms[c.S.ClassroomId].RoomNo}: {c.S.SubjectCode} ({c.S.FacultyName}){PartText(c.Part)}"))
            });

        return new OperationResult(true, chosen.Count == 0
            ? $"No classes are assigned to {me.FullName} on {FloorText(building, floor)}."
            : $"{chosen.Count} class{(chosen.Count == 1 ? "" : "es")} on {FloorText(building, floor)} assigned to {me.FullName}.");
    }
}
