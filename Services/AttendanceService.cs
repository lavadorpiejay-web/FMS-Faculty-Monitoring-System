using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record DutyView(string Id, string Title, string Detail, string State);

/// <summary>
/// READS Scholars see their duty hours (their own vacant times) and the classes of the school they are assigned to that fall inside them.
/// Every class is checked twice: 5 minutes after it starts and 5 minutes before it ends.
///   In the room at the first check                       -> Present (the end check confirms they stayed).
///   Not in the room at the first check, in it at the end -> Late.
///   Not in the room at the end check                     -> Absent, wherever the faculty is (office included).
/// </summary>
public sealed class AttendanceService
{
    public const int CheckMinutes = 5;

    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly ClockService _clock;
    private readonly AuditService _audit;

    public AttendanceService(JsonStorageService storage, SessionService session, ClockService clock, AuditService audit)
    {
        _storage = storage;
        _session = session;
        _clock = clock;
        _audit = audit;
    }

    private UserAccount? Reads => _session.CurrentUser is { Role: UserRole.ReadsScholar } u ? u : null;
    private static OperationResult Fail(string message) => new(false, message);
    private static string Hm(int minutes) => TimeFormat.To12h($"{minutes / 60:00}:{minutes % 60:00}");

    /// <summary>The signed-in READS Scholar, re-read from storage so an assignment made by IMS shows up without signing out.</summary>
    private async Task<UserAccount?> FreshMeAsync()
    {
        var me = Reads;
        if (me is null) return null;
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return users.FirstOrDefault(u => u.Id == me.Id && u.Role == UserRole.ReadsScholar) ?? me;
    }

    private async Task<List<VacantSlot>> MyVacanciesAsync(UserAccount me)
    {
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        return all.Where(v => v.ReadsUserId == me.Id).ToList();
    }

    /// <summary>A class is in scope when it belongs to the assigned school, is today, and overlaps one of the Scholar's vacant times.</summary>
    private static bool InScope(UserAccount me, IEnumerable<VacantSlot> vacancies, ClassSchedule s, DateTime now) =>
        me.AssignedSchool.Length > 0 && s.School == me.AssignedSchool && s.Day == now.DayOfWeek
        && vacancies.Any(v => v.Day == now.DayOfWeek && ScheduleRules.Overlaps(v.Start, v.End, s.Start, s.End));

    // ------------------------------------------------------------ home

    public async Task<string> GetMonitoringSchoolAsync()
    {
        var me = await FreshMeAsync();
        return me?.AssignedSchool ?? "";
    }

    /// <summary>Today's duty hours (the Scholar's vacant times today): Current, Upcoming or Completed.</summary>
    public async Task<List<DutyView>> GetMyDutiesAsync()
    {
        var me = await FreshMeAsync();
        if (me is null) return new();
        var now = _clock.Now;
        var nowMinutes = ScheduleRules.Minutes(ScheduleRules.HHmm(now));

        var list = new List<DutyView>();
        foreach (var v in (await MyVacanciesAsync(me)).Where(v => v.Day == now.DayOfWeek).OrderBy(v => v.Start))
        {
            var state = nowMinutes >= ScheduleRules.Minutes(v.End) ? "Completed"
                      : nowMinutes >= ScheduleRules.Minutes(v.Start) ? "Current" : "Upcoming";
            list.Add(new DutyView(v.Id, TimeFormat.Range(v.Start, v.End),
                me.AssignedSchool.Length == 0 ? "No school assigned yet" : $"Monitoring: {me.AssignedSchool}", state));
        }
        return list;
    }

    /// <summary>The checks this Scholar completed today (first check, end check or both), newest first.</summary>
    public async Task<List<ListRow>> GetTodayChecksAsync()
    {
        var me = Reads;
        if (me is null) return new();
        await _storage.InitializeAsync();
        var dateText = ScheduleRules.DateText(_clock.Now);
        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);
        return logs.Where(l => l.CheckerUserId == me.Id && l.Date == dateText)
            .OrderByDescending(l => l.EndCheckUtc ?? l.StartCheckUtc ?? l.TimestampUtc)
            .Select(l =>
            {
                var parts = new List<string>();
                if (l.StartCheckUtc is not null) parts.Add($"First check {l.StartCheckUtc.Value.ToLocalTime():h:mm tt}: {(l.StartInRoom == true ? "in the room" : "not in the room")}");
                if (l.EndCheckUtc is not null) parts.Add($"End check {l.EndCheckUtc.Value.ToLocalTime():h:mm tt}: {(l.EndInRoom == true ? "in the room" : "not in the room")}");
                var title = $"{l.SubjectCode} - {l.Status}{(l.IsComplete ? "" : " (end check pending)")}";
                return new ListRow(l.Id, title, $"{l.FacultyName} | {l.RoomLabel}", string.Join(" | ", parts));
            }).ToList();
    }

    // -------------------------------------------------------- monitoring

    /// <summary>Today's classes of the assigned school inside the Scholar's vacant times, with which check (if any) can be done now.</summary>
    public async Task<List<MonitorRow>> GetMonitorRowsAsync()
    {
        var me = await FreshMeAsync();
        if (me is null || me.AssignedSchool.Length == 0) return new();
        var now = _clock.Now;
        var dateText = ScheduleRules.DateText(now);
        var nowM = ScheduleRules.Minutes(ScheduleRules.HHmm(now));

        var vacancies = await MyVacanciesAsync(me);
        var todays = vacancies.Where(v => v.Day == now.DayOfWeek).ToList();
        if (todays.Count == 0) return new();
        var inVacantTime = todays.Any(v => ScheduleRules.NowWithin(now, v.Start, v.End));

        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);

        var rows = new List<(string Start, MonitorRow Row)>();
        foreach (var s in schedules.Where(x => InScope(me, vacancies, x, now)))
        {
            var room = rooms.FirstOrDefault(r => r.Id == s.ClassroomId);
            var startOpen = ScheduleRules.Minutes(s.Start) + CheckMinutes;
            var endOpen = ScheduleRules.Minutes(s.End) - CheckMinutes;
            var endM = ScheduleRules.Minutes(s.End);
            var log = logs.FirstOrDefault(l => l.ScheduleId == s.Id && l.Date == dateText);

            var title = $"{ScheduleRules.RoomLabel(room)} | {s.SubjectCode} {ScheduleRules.Range(s.Start, s.End)}";
            var detail = $"{s.SubjectTitle} | Scheduled: {s.FacultyName}";

            string state; var stage = 0;
            if (log is { IsComplete: true })
                state = $"Done: {log.Status}. {log.Observation}";
            else if (log is null)
            {
                if (nowM < startOpen) state = $"First check opens at {Hm(startOpen)}";
                else if (nowM >= endOpen) state = "The first check was not done, so this class cannot be logged anymore";
                else if (!inVacantTime) state = "Ready, but you are outside your vacant time";
                else { state = "First check is open (5 minutes after the start)"; stage = 1; }
            }
            else
            {
                var first = log.StartInRoom == true ? "in the room" : "not in the room";
                if (nowM < endOpen) state = $"First check done ({first}). End check opens at {Hm(endOpen)}";
                else if (nowM >= endM) state = $"Class ended without the end check. Recorded as {log.Status}";
                else if (!inVacantTime) state = $"First check done ({first}). Ready for the end check, but you are outside your vacant time";
                else { state = $"First check done ({first}). End check is open (5 minutes before the end)"; stage = 2; }
            }
            rows.Add((s.Start + "|" + title, new MonitorRow(s.Id, title, detail, state, stage > 0, stage)));
        }
        return rows.OrderBy(r => r.Start).Select(r => r.Row).ToList();
    }

    public async Task<List<ListRow>> GetMyLogsAsync(int count)
    {
        var me = Reads;
        if (me is null) return new();
        await _storage.InitializeAsync();
        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);
        return logs.Where(l => l.CheckerUserId == me.Id).OrderByDescending(l => l.TimestampUtc).Take(count)
            .Select(l => new ListRow(l.Id, $"{l.SubjectCode} - {l.Status}{(l.IsComplete ? "" : " (pending)")}",
                $"{l.FacultyName} | {l.Date} | {l.Observation}")).ToList();
    }

    // -------------------------------------------------------------- checks

    /// <summary>Records the first check (no log yet for this class today) or the end check (a first check exists).</summary>
    public async Task<OperationResult> CheckAsync(string scheduleId, bool inRoom, string? notes)
    {
        var me = await FreshMeAsync();
        if (me is null) return Fail("Only READS Scholars can log attendance.");
        var now = _clock.Now;
        var dateText = ScheduleRules.DateText(now);
        var nowM = ScheduleRules.Minutes(ScheduleRules.HHmm(now));
        notes = (notes ?? "").Trim();

        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = schedules.FirstOrDefault(x => x.Id == scheduleId);
        if (s is null) return Fail("That class was not found.");
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        var room = rooms.FirstOrDefault(r => r.Id == s.ClassroomId);
        var vacancies = await MyVacanciesAsync(me);

        // A class outside the assigned school or the Scholar's vacant times is refused and flagged for IMS Personnel.
        if (!InScope(me, vacancies, s, now))
        {
            await AddIrregularityAsync(me, IrregularityKind.OutsideAssignment, s, room, null,
                "Tried to log a class outside the assigned school or vacant times.", now, true,
                "Entry does not match the checker's assigned schedule");
            return Fail("This class is not in your assigned school and vacant times. The attempt was flagged for IMS Personnel.");
        }
        if (!vacancies.Any(v => v.Day == now.DayOfWeek && ScheduleRules.NowWithin(now, v.Start, v.End)))
            return Fail("You can check classes only during your vacant time.");

        var startOpen = ScheduleRules.Minutes(s.Start) + CheckMinutes;
        var endOpen = ScheduleRules.Minutes(s.End) - CheckMinutes;
        var endM = ScheduleRules.Minutes(s.End);

        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);
        var log = logs.FirstOrDefault(l => l.ScheduleId == s.Id && l.Date == dateText);

        if (log is null)
        {
            if (nowM < startOpen) return Fail($"The first check opens at {Hm(startOpen)}, 5 minutes after the class starts.");
            if (nowM >= endOpen) return Fail("The first check window has closed.");

            var clash = logs.FirstOrDefault(l => l.Date == dateText && l.ScheduleId != s.Id
                && ScheduleRules.Overlaps(l.ScheduledStart, l.ScheduledEnd, s.Start, s.End)
                && (l.FacultyUserId == s.FacultyUserId || l.ClassroomId == s.ClassroomId));
            if (clash is not null)
                return Fail($"This conflicts with an entry already logged for {clash.SubjectCode} ({clash.FacultyName}) in the same time slot.");

            logs.Add(new AttendanceLog
            {
                Id = ScheduleRules.NewId("L"), ScheduleId = s.Id, FacultyUserId = s.FacultyUserId, FacultyName = s.FacultyName,
                SubjectCode = s.SubjectCode, SubjectTitle = s.SubjectTitle, ClassroomId = s.ClassroomId,
                RoomLabel = ScheduleRules.RoomLabel(room), Campus = room is null ? "" : ScheduleRules.CampusOf(room),
                School = s.School, Department = s.Department, Building = room?.Building ?? "",
                Date = dateText, ScheduledStart = s.Start, ScheduledEnd = s.End,
                Status = inRoom ? AttendanceStatus.Present : AttendanceStatus.Absent,   // provisional until the end check
                CheckerUserId = me.Id, CheckerName = me.FullName, TimestampUtc = DateTime.UtcNow,
                StartInRoom = inRoom, StartCheckUtc = DateTime.UtcNow,
                Observation = inRoom ? "Faculty in the classroom at the first check" : "Faculty not in the classroom at the first check",
                Notes = notes
            });
            await _storage.SaveAsync(JsonStorageService.AttendanceFile, logs);
            return new OperationResult(true, inRoom
                ? $"First check saved: {s.FacultyName} is in the room. Do the end check from {Hm(endOpen)}."
                : $"First check saved: {s.FacultyName} is not in the room. Do the end check from {Hm(endOpen)}.");
        }

        if (log.IsComplete) return Fail($"Already finished: {log.Status}.");
        if (nowM < endOpen) return Fail($"The end check opens at {Hm(endOpen)}, 5 minutes before the class ends.");
        if (nowM >= endM) return Fail("The class has already ended.");

        AttendanceStatus status;
        string observation;
        if (log.StartInRoom == true)
        {
            status = AttendanceStatus.Present;
            observation = inRoom ? "Faculty in the classroom at the start and at the end"
                                 : "Faculty was in the classroom at the start but not at the end (left early)";
        }
        else
        {
            status = inRoom ? AttendanceStatus.Late : AttendanceStatus.Absent;
            observation = inRoom ? "Faculty not in the classroom at the start, arrived before the end"
                                 : "Faculty not in the classroom at the start or at the end";
        }

        var allNotes = string.Join(" | ", new[] { log.Notes, notes }.Where(n => n.Length > 0));
        var index = logs.FindIndex(l => l.Id == log.Id);
        logs[index] = log with
        {
            Status = status, EndInRoom = inRoom, EndCheckUtc = DateTime.UtcNow, TimestampUtc = DateTime.UtcNow,
            Observation = observation, Notes = allNotes
        };
        await _storage.SaveAsync(JsonStorageService.AttendanceFile, logs);
        return new OperationResult(true, $"{s.SubjectCode} ({s.FacultyName}) recorded as {status}.");
    }

    // ------------------------------------------------------- irregularities

    private async Task AddIrregularityAsync(UserAccount me, IrregularityKind kind, ClassSchedule s, Classroom? expectedRoom,
        Classroom? observedRoom, string notes, DateTime now, bool flagged, string flagReason)
    {
        var list = await _storage.LoadAsync<IrregularityReport>(JsonStorageService.IrregularitiesFile);
        list.Add(new IrregularityReport
        {
            Id = ScheduleRules.NewId("I"), Kind = kind, ScheduleId = s.Id, FacultyName = s.FacultyName,
            ScheduleLabel = ScheduleRules.ScheduleLabel(s), ExpectedRoom = ScheduleRules.RoomLabel(expectedRoom),
            ObservedRoom = observedRoom is null ? "" : ScheduleRules.RoomLabel(observedRoom), Notes = notes,
            Date = ScheduleRules.DateText(now), Flagged = flagged, FlagReason = flagReason,
            CheckerUserId = me.Id, CheckerName = me.FullName, TimestampUtc = DateTime.UtcNow
        });
        await _storage.SaveAsync(JsonStorageService.IrregularitiesFile, list);
    }

    public async Task<OperationResult> ReportAsync(IrregularityKind kind, string scheduleId, string? observedRoomId, string notes)
    {
        var me = await FreshMeAsync();
        if (me is null) return Fail("Only READS Scholars can report irregularities.");
        if (kind == IrregularityKind.OutsideAssignment) return Fail("Choose what you observed.");
        var now = _clock.Now;

        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = schedules.FirstOrDefault(x => x.Id == scheduleId);
        if (s is null) return Fail("Choose a class.");
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        var expected = rooms.FirstOrDefault(r => r.Id == s.ClassroomId);

        Classroom? observed = null;
        if (kind == IrregularityKind.UnapprovedRoom)
        {
            observed = rooms.FirstOrDefault(r => r.Id == observedRoomId);
            if (observed is null) return Fail("Choose the room where the class was held.");
            if (observed.Id == s.ClassroomId) return Fail("That is the assigned room. Choose the room you actually saw.");
        }

        var inScope = InScope(me, await MyVacanciesAsync(me), s, now);
        await AddIrregularityAsync(me, kind, s, expected, observed, notes.Trim(), now, !inScope,
            inScope ? "" : "Report does not match the checker's assigned schedule");
        return new OperationResult(true, inScope
            ? "Irregularity report submitted to IMS Personnel."
            : "Report submitted and flagged, because the class is outside your assigned school or vacant times.");
    }

    // ------------------------------------------------- IMS oversight lists

    public async Task<List<AttendanceLog>> GetRecentLogsForImsAsync(int count)
    {
        if (_session.Role != UserRole.ImsPersonnel) return new();
        await _storage.InitializeAsync();
        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);
        return logs.OrderByDescending(l => l.TimestampUtc).Take(count).ToList();
    }

    public async Task<List<IrregularityReport>> GetIrregularitiesForImsAsync(int count)
    {
        if (_session.Role != UserRole.ImsPersonnel) return new();
        await _storage.InitializeAsync();
        var list = await _storage.LoadAsync<IrregularityReport>(JsonStorageService.IrregularitiesFile);
        return list.OrderByDescending(r => r.Flagged).ThenByDescending(r => r.TimestampUtc).Take(count).ToList();
    }
}
