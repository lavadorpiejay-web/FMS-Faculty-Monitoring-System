using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>IMS Personnel record READS Scholars' vacant times and assign duty schedules and monitoring areas inside them.</summary>
public sealed class DutyService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;
    private readonly ScholarAssignmentService _scholars;

    public DutyService(JsonStorageService storage, SessionService session, AuditService audit, ScholarAssignmentService scholars)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
        _scholars = scholars;
    }

    private bool IsIms => _session.Role == UserRole.ImsPersonnel;
    private static OperationResult Fail(string message) => new(false, message);

    public Task<List<ScholarInfo>> GetScholarsAsync() => _scholars.GetScholarsAsync();

    public async Task<List<DutyAssignment>> GetDutiesAsync()
    {
        if (!IsIms) return new();
        await _storage.InitializeAsync();
        var list = await _storage.LoadAsync<DutyAssignment>(JsonStorageService.DutiesFile);
        return list.OrderBy(d => d.ReadsName).ThenBy(d => d.Day == DayOfWeek.Sunday ? 7 : (int)d.Day).ThenBy(d => d.Start).ToList();
    }

    public async Task<List<VacantSlot>> GetVacanciesAsync(string readsUserId)
    {
        if (!IsIms) return new();
        await _storage.InitializeAsync();
        var list = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        return list.Where(v => v.ReadsUserId == readsUserId)
                   .OrderBy(v => v.Day == DayOfWeek.Sunday ? 7 : (int)v.Day).ThenBy(v => v.Start).ToList();
    }

    private async Task<UserAccount?> FindScholarAsync(string userId)
    {
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return users.FirstOrDefault(u => u.Id == userId && u.Role == UserRole.ReadsScholar && u.IsUsable);
    }

    public async Task<OperationResult> AddVacancyAsync(string readsUserId, string dayName, string? start, string? end)
    {
        if (!IsIms) return Fail("Only IMS Personnel can record vacant times.");
        var timeError = ScheduleRules.CheckTimes(start, end);
        if (timeError is not null) return Fail(timeError);

        await _storage.InitializeAsync();
        var scholar = await FindScholarAsync(readsUserId);
        if (scholar is null) return Fail("That READS Scholar was not found.");

        var day = ScheduleRules.ParseDay(dayName);
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        if (all.Any(v => v.ReadsUserId == readsUserId && v.Day == day && ScheduleRules.Overlaps(v.Start, v.End, start!, end!)))
            return Fail("That overlaps a vacant time already recorded for this day.");

        all.Add(new VacantSlot { Id = ScheduleRules.NewId("V"), ReadsUserId = readsUserId, Day = day, Start = start!, End = end! });
        await _storage.SaveAsync(JsonStorageService.VacanciesFile, all);
        await _audit.LogAsync("Vacant time recorded", $"{scholar.FullName}: {day} {ScheduleRules.Range(start!, end!)}");
        return new OperationResult(true, $"Vacant time saved for {scholar.FullName}.");
    }

    public async Task<OperationResult> RemoveVacancyAsync(string slotId)
    {
        if (!IsIms) return Fail("Only IMS Personnel can change vacant times.");
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var slot = all.FirstOrDefault(v => v.Id == slotId);
        if (slot is null) return Fail("That vacant time was not found.");

        var duties = await _storage.LoadAsync<DutyAssignment>(JsonStorageService.DutiesFile);
        var others = all.Where(v => v.Id != slotId).ToList();
        if (duties.Any(d => d.ReadsUserId == slot.ReadsUserId && d.Day == slot.Day
                            && ScheduleRules.Contains(slot.Start, slot.End, d.Start, d.End)
                            && !others.Any(v => v.ReadsUserId == d.ReadsUserId && v.Day == d.Day && ScheduleRules.Contains(v.Start, v.End, d.Start, d.End))))
            return Fail("A duty depends on this vacant time. Change or remove the duty first.");

        all.Remove(slot);
        await _storage.SaveAsync(JsonStorageService.VacanciesFile, all);
        await _audit.LogAsync("Vacant time removed", $"{slot.Day} {ScheduleRules.Range(slot.Start, slot.End)}");
        return new OperationResult(true, "Vacant time removed.");
    }

    /// <summary>Creates a duty (dutyId null) or modifies / reassigns an existing one.</summary>
    public async Task<OperationResult> SaveDutyAsync(string? dutyId, string readsUserId, string? campus,
        string building, string floor, string dayName, string? start, string? end)
    {
        if (!IsIms) return Fail("Only IMS Personnel can assign duties.");
        var timeError = ScheduleRules.CheckTimes(start, end);
        if (timeError is not null) return Fail(timeError);
        if (campus is null || !AcademicCatalog.Campuses.Contains(campus)) return Fail("Choose a campus.");
        building = (building ?? "").Trim();
        floor = (floor ?? "").Trim();

        await _storage.InitializeAsync();
        var scholar = await FindScholarAsync(readsUserId);
        if (scholar is null) return Fail("That READS Scholar was not found.");

        // The monitoring area is a place: check it against the classrooms that actually exist, so a typo cannot silently match nothing.
        var campusRooms = (await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile))
            .Where(r => string.Equals(ScheduleRules.CampusOf(r), campus, StringComparison.OrdinalIgnoreCase)).ToList();
        if (campusRooms.Count == 0) return Fail($"No classrooms are recorded for {campus} yet. Add them on the Rooms tab first.");
        var rooms = campusRooms;
        if (building.Length > 0)
        {
            rooms = campusRooms.Where(r => string.Equals(r.Building.Trim(), building, StringComparison.OrdinalIgnoreCase)).ToList();
            if (rooms.Count == 0)
            {
                var known = campusRooms.Select(r => r.Building.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(b => b);
                return Fail($"No classroom in building \"{building}\" on {campus}. Buildings there: {string.Join(", ", known)}.");
            }
        }
        if (floor.Length > 0 && !rooms.Any(r => string.Equals(r.Floor.Trim(), floor, StringComparison.OrdinalIgnoreCase)))
        {
            var floors = rooms.Select(r => r.Floor.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f);
            return Fail($"No classroom on floor \"{floor}\" there. Floors: {string.Join(", ", floors)}.");
        }

        var day = ScheduleRules.ParseDay(dayName);
        var vacancies = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        if (!vacancies.Any(v => v.ReadsUserId == readsUserId && v.Day == day && ScheduleRules.Contains(v.Start, v.End, start!, end!)))
            return Fail($"A duty must fit inside one of {scholar.FullName}'s vacant times on {day}. Record that vacant time first.");

        var duties = await _storage.LoadAsync<DutyAssignment>(JsonStorageService.DutiesFile);
        if (duties.Any(d => d.Id != dutyId && d.ReadsUserId == readsUserId && d.Day == day && ScheduleRules.Overlaps(d.Start, d.End, start!, end!)))
            return Fail($"{scholar.FullName} already has a duty that overlaps this time.");

        var duty = new DutyAssignment
        {
            Id = dutyId ?? ScheduleRules.NewId("D"), ReadsUserId = readsUserId, ReadsName = scholar.FullName,
            Campus = campus, Building = building, Floor = floor,
            Day = day, Start = start!, End = end!
        };

        var index = dutyId is null ? -1 : duties.FindIndex(d => d.Id == dutyId);
        if (dutyId is not null && index < 0) return Fail("That duty was not found.");

        if (index >= 0)
        {
            var before = duties[index];
            duties[index] = duty;
            await _storage.SaveAsync(JsonStorageService.DutiesFile, duties);
            await _audit.LogAsync("Duty modified", $"Before: {before.ReadsName}, {ScheduleRules.DutyText(before)}. After: {duty.ReadsName}, {ScheduleRules.DutyText(duty)}");
            return new OperationResult(true, $"Duty updated for {scholar.FullName}.");
        }

        duties.Add(duty);
        await _storage.SaveAsync(JsonStorageService.DutiesFile, duties);
        await _audit.LogAsync("Duty assigned", $"{duty.ReadsName}, {ScheduleRules.DutyText(duty)}");
        return new OperationResult(true, $"Duty assigned to {scholar.FullName}.");
    }

    public async Task<OperationResult> RemoveDutyAsync(string dutyId)
    {
        if (!IsIms) return Fail("Only IMS Personnel can remove duties.");
        await _storage.InitializeAsync();
        var duties = await _storage.LoadAsync<DutyAssignment>(JsonStorageService.DutiesFile);
        var duty = duties.FirstOrDefault(d => d.Id == dutyId);
        if (duty is null) return Fail("That duty was not found.");
        duties.Remove(duty);
        await _storage.SaveAsync(JsonStorageService.DutiesFile, duties);
        await _audit.LogAsync("Duty removed", $"{duty.ReadsName}, {ScheduleRules.DutyText(duty)}");
        return new OperationResult(true, "Duty removed.");
    }
}
