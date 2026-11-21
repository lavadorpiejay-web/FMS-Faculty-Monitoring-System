using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// In-app notifications (the bell in the top bar). Written when a request is sent, approved, declined or applied,
/// and shown to the person or role it is addressed to until they open it or mark everything as read.
/// A notification addressed to a role is one shared inbox for that role (IMS Personnel; the Chairperson of a department; the Dean of a school).
/// </summary>
public sealed class NotificationService
{
    // Categories, so a notice can be cleared when its job is done.
    public const string ForIms = "ims";             // approved request waiting for IMS Personnel
    public const string ForApprover = "approver";   // new request waiting for a Chairperson or Dean
    public const string ForRequester = "requester"; // decision about the Faculty member's own request

    private const int MaxEntries = 300;
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;

    public NotificationService(JsonStorageService storage, SessionService session)
    {
        _storage = storage;
        _session = session;
    }

    /// <summary>Raised after anything is added or read, so every bell can refresh its count.</summary>
    public event Action? Changed;

    private async Task<List<AppNotification>> LoadAsync()
    {
        await _storage.InitializeAsync();
        return await _storage.LoadAsync<AppNotification>(JsonStorageService.NotificationsFile);
    }

    private async Task SaveAsync(List<AppNotification> list)
    {
        await _storage.SaveAsync(JsonStorageService.NotificationsFile, list);
        Changed?.Invoke();
    }

    private static bool IsFor(AppNotification n, UserAccount me)
    {
        if (n.RecipientUserId.Length > 0) return n.RecipientUserId == me.Id;
        if (n.RecipientRole is null) return me.Role == UserRole.ImsPersonnel;   // older records
        return n.RecipientRole == me.Role
            && (n.RecipientSchool.Length == 0 || n.RecipientSchool == me.School)
            && (n.RecipientDepartment.Length == 0 || n.RecipientDepartment == me.Department);
    }

    public async Task NotifyAsync(AppNotification n)
    {
        var list = await LoadAsync();
        list.Add(n with { Id = ScheduleRules.NewId("N"), CreatedUtc = DateTime.UtcNow, IsRead = false });
        if (list.Count > MaxEntries) list = list.Skip(list.Count - MaxEntries).ToList();
        await SaveAsync(list);
    }

    /// <summary>The signed-in person's notifications, newest first.</summary>
    public async Task<List<AppNotification>> GetMineAsync(int max = 60)
    {
        var me = _session.CurrentUser;
        if (me is null) return new();
        return (await LoadAsync()).Where(n => IsFor(n, me)).OrderByDescending(n => n.CreatedUtc).Take(max).ToList();
    }

    public async Task<int> UnreadCountAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return 0;
        return (await LoadAsync()).Count(n => !n.IsRead && IsFor(n, me));
    }

    public async Task MarkReadAsync(string id)
    {
        var list = await LoadAsync();
        if (!list.Any(n => n.Id == id && !n.IsRead)) return;
        await SaveAsync(list.Select(n => n.Id == id ? n with { IsRead = true } : n).ToList());
    }

    public async Task MarkAllReadAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return;
        var list = await LoadAsync();
        if (!list.Any(n => !n.IsRead && IsFor(n, me))) return;
        await SaveAsync(list.Select(n => !n.IsRead && IsFor(n, me) ? n with { IsRead = true } : n).ToList());
    }

    /// <summary>The request moved on (decided, applied or declined), so its notices of these categories are done.</summary>
    public async Task MarkRequestHandledAsync(string requestId, params string[] categories)
    {
        var list = await LoadAsync();
        bool Hit(AppNotification n) => n.RequestId == requestId && !n.IsRead && categories.Contains(n.Category);
        if (!list.Any(Hit)) return;
        await SaveAsync(list.Select(n => Hit(n) ? n with { IsRead = true } : n).ToList());
    }
}
