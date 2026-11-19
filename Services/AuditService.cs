using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Records every schedule, room, duty and approval change so it can be traced later.</summary>
public sealed class AuditService
{
    private const int MaxEntries = 500;
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;

    public AuditService(JsonStorageService storage, SessionService session)
    {
        _storage = storage;
        _session = session;
    }

    public async Task LogAsync(string action, string details)
    {
        await _storage.InitializeAsync();
        var list = await _storage.LoadAsync<AuditEntry>(JsonStorageService.AuditFile);
        var me = _session.CurrentUser;
        list.Add(new AuditEntry
        {
            Id = ScheduleRules.NewId("A"), TimestampUtc = DateTime.UtcNow,
            ActorUserId = me?.Id ?? "", ActorName = me?.FullName ?? "", ActorRole = me?.Role.ToString() ?? "",
            Action = action, Details = details
        });
        if (list.Count > MaxEntries) list = list.Skip(list.Count - MaxEntries).ToList();
        await _storage.SaveAsync(JsonStorageService.AuditFile, list);
    }

    /// <summary>IMS Personnel only.</summary>
    public async Task<List<AuditEntry>> RecentAsync(int count)
    {
        if (_session.Role != UserRole.ImsPersonnel) return new();
        await _storage.InitializeAsync();
        var list = await _storage.LoadAsync<AuditEntry>(JsonStorageService.AuditFile);
        return list.OrderByDescending(a => a.TimestampUtc).Take(count).ToList();
    }
}
