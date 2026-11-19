using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Class schedules and classroom records. IMS Personnel encode them; every role sees the part that concerns it.</summary>
public sealed class ScheduleService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;

    public ScheduleService(JsonStorageService storage, SessionService session, AuditService audit)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
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

    /// <summary>IMS only: the Faculty accounts a schedule can be encoded for.</summary>
    public async Task<List<UserAccount>> GetFacultyAsync()
    {
        if (!IsIms) return new();
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return users.Where(u => u.Role == UserRole.Faculty && u.IsUsable).OrderBy(u => u.FullName).ToList();
    }

    /// <summary>The first clash with the candidate: same room or same Faculty at an overlapping time on the same day.</summary>
    public static string? FindConflict(IEnumerable<ClassSchedule> all, ClassSchedule candidate)
    {
        foreach (var s in all.Where(s => s.Id != candidate.Id && s.Day == candidate.Day
                                         && ScheduleRules.Overlaps(s.Start, s.End, candidate.Start, candidate.End)))
        {
            if (s.ClassroomId == candidate.ClassroomId)
                return $"That room is already used by {s.SubjectCode} ({s.FacultyName}) on {s.Day}, {ScheduleRules.Range(s.Start, s.End)}.";
            if (s.FacultyUserId == candidate.FacultyUserId)
                return $"{candidate.FacultyName} already teaches {s.SubjectCode} on {s.Day}, {ScheduleRules.Range(s.Start, s.End)}.";
        }
        return null;
    }

    public async Task<OperationResult> AddAsync(string facultyUserId, string subjectCode, string classroomId,
        string dayName, string? start, string? end)
    {
        if (!IsIms) return Fail("Only IMS Personnel can encode schedules.");
        var timeError = ScheduleRules.CheckTimes(start, end);
        if (timeError is not null) return Fail(timeError);

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var faculty = users.FirstOrDefault(u => u.Id == facultyUserId && u.Role == UserRole.Faculty && u.IsUsable);
        if (faculty is null) return Fail("That Faculty member was not found.");

        var subject = (await GetSubjectsAsync()).FirstOrDefault(s => s.Code == subjectCode && s.Department == faculty.Department);
        if (subject is null) return Fail("Choose a subject of the Faculty member's department.");

        var room = (await GetRoomsAsync()).FirstOrDefault(r => r.Id == classroomId);
        if (room is null) return Fail("Choose a classroom.");

        var all = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var candidate = new ClassSchedule
        {
            Id = ScheduleRules.NewId("S"), FacultyUserId = faculty.Id, FacultyName = faculty.FullName,
            School = faculty.School, Department = faculty.Department,
            SubjectCode = subject.Code, SubjectTitle = subject.Title, ClassroomId = room.Id,
            Day = ScheduleRules.ParseDay(dayName), Start = start!, End = end!
        };
        var conflict = FindConflict(all, candidate);
        if (conflict is not null) return Fail(conflict);

        all.Add(candidate);
        await _storage.SaveAsync(JsonStorageService.SchedulesFile, all);
        await _audit.LogAsync("Schedule encoded", $"{candidate.FacultyName}: {ScheduleRules.ScheduleLabel(candidate)} in {ScheduleRules.RoomLabel(room)}");
        return new OperationResult(true, $"Saved {candidate.SubjectCode} for {candidate.FacultyName}.");
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

    public async Task<OperationResult> AddRoomAsync(string? campus, string? school, string building, string floor, string roomNo, string capacityText)
    {
        if (!IsIms) return Fail("Only IMS Personnel can manage classrooms.");
        if (campus is null || !AcademicCatalog.Campuses.Contains(campus)) return Fail("Choose a campus.");
        if (!AcademicCatalog.IsValidSchool(school)) return Fail("Choose the school that owns the room (its Dean approves room changes).");
        building = building.Trim(); floor = floor.Trim(); roomNo = roomNo.Trim();
        if (building.Length == 0 || roomNo.Length == 0) return Fail("Enter the building and the room number.");
        if (!int.TryParse(capacityText.Trim(), out var capacity) || capacity < 1 || capacity > 500)
            return Fail("Capacity must be a number from 1 to 500.");

        await _storage.InitializeAsync();
        var rooms = await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile);
        if (rooms.Any(r => string.Equals(ScheduleRules.CampusOf(r), campus, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(r.Building, building, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(r.RoomNo, roomNo, StringComparison.OrdinalIgnoreCase)))
            return Fail("That room already exists.");

        var next = rooms.Select(r => r.Id.StartsWith("R-") && int.TryParse(r.Id[2..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var room = new Classroom { Id = $"R-{next:000}", Campus = campus, School = school!, Building = building, Floor = floor, RoomNo = roomNo, Capacity = capacity };
        rooms.Add(room);
        await _storage.SaveAsync(JsonStorageService.ClassroomsFile, rooms);
        await _audit.LogAsync("Classroom added", $"{ScheduleRules.RoomLabel(room)} (floor {(floor.Length > 0 ? floor : "-")}, capacity {capacity}, {school})");
        return new OperationResult(true, $"Added {ScheduleRules.RoomLabel(room)}.");
    }
}
