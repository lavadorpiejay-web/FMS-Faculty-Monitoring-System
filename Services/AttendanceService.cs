using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record DutyView(string Id, string Title, string Detail, string State);

/// <summary>READS Scholars see their duties and the classes in their monitoring area, log attendance and report irregularities.</summary>
public sealed class AttendanceService
{
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

    private async Task<List<DutyAssignment>> MyDutiesAsync(UserAccount me)
    {
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<DutyAssignment>(JsonStorageService.DutiesFile);
        return all.Where(d => d.ReadsUserId == me.Id).ToList();
    }

    private static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

    // ------------------------------------------------------------ dashboard

    /// <summary>Current (on duty now), Upcoming (later today or on a later day) and Completed (ended earlier today).</summary>
    public async Task<List<DutyView>> GetMyDutiesAsync()
    {
        var me = Reads;
        if (me is null) return new();
        var now = _clock.Now;
        var nowMinutes = ScheduleRules.Minutes(ScheduleRules.HHmm(now));

        var rows = new List<(int Rank, int Order, DutyView View)>();
        foreach (var d in await MyDutiesAsync(me))
        {
            string state;
            if (d.Day != now.DayOfWeek) state = "Upcoming";
            else if (nowMinutes >= ScheduleRules.Minutes(d.End)) state = "Completed";
            else if (nowMinutes >= ScheduleRules.Minutes(d.Start)) state = "Current";
            else state = "Upcoming";

            var daysAway = ((int)d.Day - (int)now.DayOfWeek + 7) % 7;
            var rank = state == "Current" ? 0 : state == "Upcoming" ? 1 : 2;
            rows.Add((rank, daysAway * 2000 + ScheduleRules.Minutes(d.Start),
                new DutyView(d.Id, $"{d.Day} {ScheduleRules.Range(d.Start, d.End)}", $"{d.Campus} | {ScheduleRules.AreaText(d)}", state)));
        }
        return rows.OrderBy(r => r.Rank).ThenBy(r => r.Order).Select(r => r.View).ToList();
    }

    /// <summary>Today's classes inside the Scholar's monitoring area and duty hours, with whether each can be logged now.</summary>
    public async Task<List<MonitorRow>> GetMonitorRowsAsync()
    {
        var me = Reads;
        if (me is null) return new();
        var now = _clock.Now;
        var today = now.DayOfWeek;
        var dateText = ScheduleRules.DateText(now);

        var duties = (await MyDutiesAsync(me)).Where(d => d.Day == today).ToList();
        if (duties.Count == 0) return new();

        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);

        var rows = new List<(string Start, MonitorRow Row)>();
        foreach (var s in schedules.Where(x => x.Day == today))
        {
            var room = rooms.FirstOrDefault(r => r.Id == s.ClassroomId);
            var covering = duties.Where(d => ScheduleRules.DutyCovers(d, s, room) && ScheduleRules.Overlaps(d.Start, d.End, s.Start, s.End)).ToList();
            if (covering.Count == 0) continue;

            var title = $"{s.SubjectCode} {ScheduleRules.Range(s.Start, s.End)} - {s.FacultyName}";
            var detail = $"{s.SubjectTitle} | {ScheduleRules.RoomLabel(room)}";
            var logged = logs.FirstOrDefault(l => l.ScheduleId == s.Id && l.Date == dateText);

            string state;
            var canLog = false;
            if (logged is not null) state = $"Logged: {logged.Status} (by {logged.CheckerName})";
            else if (ScheduleRules.Minutes(ScheduleRules.HHmm(now)) < ScheduleRules.Minutes(s.Start)) state = "Not started yet";
            else if (!ScheduleRules.NowWithin(now, s.Start, s.End)) state = "Class ended, not logged";
            else if (!covering.Any(d => ScheduleRules.NowWithin(now, d.Start, d.End))) state = "In session, but your duty window is not active";
            else { state = "In session - ready to log"; canLog = true; }

            rows.Add((s.Start, new MonitorRow(s.Id, title, detail, state, canLog)));
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
            .Select(l => new ListRow(l.Id, $"{l.SubjectCode} - {l.Status}",
                $"{l.FacultyName} | {l.Date} | logged {l.TimestampUtc.ToLocalTime():h:mm tt}")).ToList();
    }

    // -------------------------------------------------------------- logging

    private static bool InScope(IEnumerable<DutyAssignment> duties, ClassSchedule s, Classroom? room, DateTime now) =>
        s.Day == now.DayOfWeek
        && duties.Any(d => d.Day == now.DayOfWeek && ScheduleRules.DutyCovers(d, s, room) && ScheduleRules.Overlaps(d.Start, d.End, s.Start, s.End));

    public async Task<OperationResult> LogAsync(string scheduleId, AttendanceStatus status)
    {
        var me = Reads;
        if (me is null) return Fail("Only READS Scholars can log attendance.");
        await _storage.InitializeAsync();
        var now = _clock.Now;
        var dateText = ScheduleRules.DateText(now);

        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var s = schedules.FirstOrDefault(x => x.Id == scheduleId);
        if (s is null) return Fail("That class was not found.");
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        var room = rooms.FirstOrDefault(r => r.Id == s.ClassroomId);
        var duties = await MyDutiesAsync(me);

        // An entry that does not match the checker's assigned schedule is refused and flagged for IMS Personnel.
        if (!InScope(duties, s, room, now))
        {
            await AddIrregularityAsync(me, IrregularityKind.OutsideAssignment, s, room, null,
                $"Tried to log {status} for a class outside the assigned area or duty hours.", now, true,
                "Entry does not match the checker's assigned schedule");
            return Fail("This class is not in your assigned monitoring area and duty hours. The attempt was flagged for IMS Personnel.");
        }

        if (!ScheduleRules.NowWithin(now, s.Start, s.End))
            return Fail($"You can log attendance only during class hours ({ScheduleRules.Range(s.Start, s.End)}).");
        if (!duties.Any(d => d.Day == now.DayOfWeek && ScheduleRules.NowWithin(now, d.Start, d.End)
                             && ScheduleRules.DutyCovers(d, s, room)))
            return Fail("Your duty window is not active right now.");

        var logs = await _storage.LoadAsync<AttendanceLog>(JsonStorageService.AttendanceFile);
        var duplicate = logs.FirstOrDefault(l => l.ScheduleId == s.Id && l.Date == dateText);
        if (duplicate is not null)
            return Fail($"Already logged as {duplicate.Status} by {duplicate.CheckerName}.");
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
            Date = dateText, ScheduledStart = s.Start, ScheduledEnd = s.End, Status = status,
            CheckerUserId = me.Id, CheckerName = me.FullName, TimestampUtc = DateTime.UtcNow
        });
        await _storage.SaveAsync(JsonStorageService.AttendanceFile, logs);
        return new OperationResult(true, $"{s.SubjectCode} ({s.FacultyName}) logged as {status}.");
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
        var me = Reads;
        if (me is null) return Fail("Only READS Scholars can report irregularities.");
        if (kind == IrregularityKind.OutsideAssignment) return Fail("Choose what you observed.");
        await _storage.InitializeAsync();
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

        var inScope = InScope(await MyDutiesAsync(me), s, expected, now);
        await AddIrregularityAsync(me, kind, s, expected, observed, notes.Trim(), now, !inScope,
            inScope ? "" : "Report does not match the checker's assigned schedule");
        return new OperationResult(true, inScope
            ? "Irregularity report submitted to IMS Personnel."
            : "Report submitted and flagged, because the class is outside your assigned area or duty hours.");
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
