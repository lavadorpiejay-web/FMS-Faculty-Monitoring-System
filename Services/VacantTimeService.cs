using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>A READS Scholar records their own vacant (no-class) times. Those are the duty hours: classes of the assigned school that overlap them are monitored.</summary>
public sealed class VacantTimeService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;
    private readonly LocalAlertService _alerts;

    public VacantTimeService(JsonStorageService storage, SessionService session, AuditService audit, LocalAlertService alerts)
    {
        _storage = storage;
        _session = session;
        _audit = audit;
        _alerts = alerts;
    }

    private UserAccount? Reads => _session.CurrentUser is { Role: UserRole.ReadsScholar } u ? u : null;
    private static OperationResult Fail(string message) => new(false, message);
    private static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

    public async Task<List<VacantSlot>> GetMineAsync()
    {
        var me = Reads;
        if (me is null) return new();
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        return all.Where(v => v.ReadsUserId == me.Id).OrderBy(v => DayOrder(v.Day)).ThenBy(v => v.Start).ToList();
    }

    /// <summary>Several days at once with the same start and end (for example Monday, Wednesday, Friday). Saved all together or not at all.</summary>
    public async Task<OperationResult> AddAsync(IReadOnlyList<DayOfWeek> days, string? start, string? end)
    {
        var me = Reads;
        if (me is null) return Fail("Only READS can record their vacant times.");
        if (days.Count == 0) return Fail("Tick at least one day.");
        var timeError = ScheduleRules.CheckTimes(start, end);
        if (timeError is not null) return Fail(timeError);

        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var mine = all.Where(v => v.ReadsUserId == me.Id).ToList();

        var overlaps = days.Where(d => mine.Any(v => v.Day == d && ScheduleRules.Overlaps(v.Start, v.End, start!, end!))).ToList();
        if (overlaps.Count > 0)
            return Fail($"That overlaps a vacant time you already saved on {string.Join(", ", overlaps)}. Remove it first or choose a different time.");

        foreach (var d in days)
            all.Add(new VacantSlot { Id = ScheduleRules.NewId("V"), ReadsUserId = me.Id, Day = d, Start = start!, End = end! });
        await _storage.SaveAsync(JsonStorageService.VacanciesFile, all);
        await _audit.LogAsync("Vacant time recorded",
            $"{me.FullName}: {string.Join(", ", days.Select(d => d.ToString()[..3]))} {TimeFormat.Range(start, end)}");
        _ = _alerts.SyncAsync();   // schedule the duty reminders for the new times
        return new OperationResult(true, $"Saved {days.Count} vacant time{(days.Count == 1 ? "" : "s")}.");
    }

    public async Task<OperationResult> RemoveAsync(string slotId)
    {
        var me = Reads;
        if (me is null) return Fail("Only READS can change their vacant times.");
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var slot = all.FirstOrDefault(v => v.Id == slotId && v.ReadsUserId == me.Id);
        if (slot is null) return Fail("That vacant time was not found.");
        all.Remove(slot);
        await _storage.SaveAsync(JsonStorageService.VacanciesFile, all);
        await _audit.LogAsync("Vacant time removed", $"{me.FullName}: {slot.Day} {TimeFormat.Range(slot.Start, slot.End)}");
        _ = _alerts.SyncAsync();   // drop the reminder of the removed time
        return new OperationResult(true, "Vacant time removed.");
    }

    /// <summary>Removes several vacant times at once (a whole day, a whole time, or the ones the Scholar picked). All or nothing.</summary>
    public async Task<OperationResult> RemoveManyAsync(IReadOnlyCollection<string> slotIds)
    {
        var me = Reads;
        if (me is null) return Fail("Only READS can change their vacant times.");
        if (slotIds.Count == 0) return Fail("Nothing was chosen to remove.");
        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
        var ids = slotIds.ToHashSet();
        var gone = all.Where(v => ids.Contains(v.Id) && v.ReadsUserId == me.Id).ToList();
        if (gone.Count == 0) return Fail("Those vacant times were not found.");
        all.RemoveAll(v => ids.Contains(v.Id) && v.ReadsUserId == me.Id);
        await _storage.SaveAsync(JsonStorageService.VacanciesFile, all);
        await _audit.LogAsync("Vacant times removed",
            $"{me.FullName}: {string.Join(", ", gone.Select(v => $"{v.Day.ToString()[..3]} {TimeFormat.Range(v.Start, v.End)}"))}");
        _ = _alerts.SyncAsync();   // drop the reminders of the removed times
        return new OperationResult(true, gone.Count == 1 ? "Vacant time removed." : $"{gone.Count} vacant times removed.");
    }
}
