using FacultyMonitoring.Models;
using Plugin.LocalNotification;

namespace FacultyMonitoring.Services;

/// <summary>
/// Phone alerts made by the app itself (Plugin.LocalNotification). They need no server, but they only reach the phone the app is on.
/// 1. Reminders, repeating weekly: READS Scholar "Duty starts in 10 minutes" (from their own vacant times) and
///    Faculty "Class starts in 10 minutes" (from their schedule).
/// 2. When the app is opened or resumed (or someone signs in), one alert if there are bell notifications that were not alerted yet.
/// Every call is wrapped so a problem with alerts can never break the app.
/// </summary>
public sealed class LocalAlertService
{
    public const int LeadMinutes = 10;
    private const int NewNoticesId = 900001;
    private const string IdsKey = "local_alert_ids";
    private const string AskedKey = "local_alert_permission_asked";

    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly NotificationService _notifications;

    public LocalAlertService(JsonStorageService storage, SessionService session, NotificationService notifications)
    {
        _storage = storage;
        _session = session;
        _notifications = notifications;
    }

    /// <summary>Sign-in, app resume, or a change to the vacant times: refresh the reminders and alert about new notifications.</summary>
    public async Task SyncAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return;
        try
        {
            if (!await EnsurePermissionAsync()) return;
            await ScheduleRemindersAsync(me);
            await AlertNewNoticesAsync(me);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Local alerts: {ex.Message}"); }
    }

    /// <summary>Sign-out: remove this person's reminders so the next account on the phone does not get them.</summary>
    public void CancelReminders()
    {
        try
        {
            var ids = Preferences.Default.Get(IdsKey, "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            if (ids.Length > 0) LocalNotificationCenter.Current.Cancel(ids);
            Preferences.Default.Remove(IdsKey);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Local alerts: {ex.Message}"); }
    }

    // Ask once. After that only check, so a "No" is not nagged about on every resume.
    private static async Task<bool> EnsurePermissionAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled()) return true;
        if (Preferences.Default.Get(AskedKey, false)) return false;
        Preferences.Default.Set(AskedKey, true);
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    // ------------------------------------------------------------ reminders

    private async Task ScheduleRemindersAsync(UserAccount session)
    {
        await _storage.InitializeAsync();
        CancelReminders();   // start clean: removed vacant times or changed classes must not keep ringing

        var reminders = new List<(string Key, DayOfWeek Day, string Start, string Title, string Text)>();

        if (session.Role == UserRole.ReadsScholar)
        {
            var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
            var school = users.FirstOrDefault(u => u.Id == session.Id)?.AssignedSchool ?? "";
            var where = school.Length > 0 ? $"Monitoring: {school}." : "No school assigned yet.";
            var slots = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);
            foreach (var v in slots.Where(v => v.ReadsUserId == session.Id))
                reminders.Add(("duty:" + v.Id, v.Day, v.Start, $"Duty starts in {LeadMinutes} minutes",
                    $"{v.Day} {TimeFormat.Range(v.Start, v.End)}. {where}"));
        }
        else if (session.Role == UserRole.Faculty)
        {
            var rooms = (await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile)).ToDictionary(r => r.Id);
            var classes = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
            foreach (var c in classes.Where(c => c.FacultyUserId == session.Id))
            {
                rooms.TryGetValue(c.ClassroomId, out var room);
                reminders.Add(("class:" + c.Id, c.Day, c.Start, $"Class starts in {LeadMinutes} minutes",
                    $"{c.SubjectCode} - {c.SubjectTitle}, {ScheduleRules.RoomLabel(room)}. {TimeFormat.Range(c.Start, c.End)}"));
            }
        }

        var scheduled = new List<int>();
        foreach (var r in reminders)
        {
            var id = StableId(r.Key);
            var request = new NotificationRequest
            {
                NotificationId = id,
                Title = r.Title,
                Description = r.Text,
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = NextOccurrence(r.Day, r.Start, LeadMinutes),
                    RepeatType = NotificationRepeat.Weekly
                }
            };
            await LocalNotificationCenter.Current.Show(request);
            scheduled.Add(id);
        }
        Preferences.Default.Set(IdsKey, string.Join(",", scheduled));
    }

    /// <summary>The next time (today or later) that is <paramref name="leadMinutes"/> before <paramref name="hhmm"/> on <paramref name="day"/>.</summary>
    internal static DateTime NextOccurrence(DayOfWeek day, string hhmm, int leadMinutes)
    {
        var now = DateTime.Now;
        var date = now.Date.AddDays(((int)day - (int)now.DayOfWeek + 7) % 7);
        var when = date.AddMinutes(ScheduleRules.Minutes(hhmm) - leadMinutes);
        return when <= now.AddSeconds(5) ? when.AddDays(7) : when;
    }

    // string.GetHashCode changes on every run, so use a fixed hash for the notification id.
    private static int StableId(string key)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var ch in key) h = (h ^ ch) * 16777619u;
            return (int)(h % 800000u) + 1000;   // 1000..800999, clear of NewNoticesId
        }
    }

    // ------------------------------------------------------ bell notifications

    private async Task AlertNewNoticesAsync(UserAccount me)
    {
        var key = $"local_alert_seen_{me.Id}";
        var seenTicks = Preferences.Default.Get(key, 0L);
        var fresh = (await _notifications.GetMineAsync())
            .Where(n => !n.IsRead && n.CreatedUtc.Ticks > seenTicks)
            .OrderByDescending(n => n.CreatedUtc).ToList();
        Preferences.Default.Set(key, DateTime.UtcNow.Ticks);
        if (fresh.Count == 0) return;

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = NewNoticesId,
            Title = fresh.Count == 1 ? fresh[0].Title : $"{fresh.Count} new notifications",
            Description = fresh.Count == 1 ? fresh[0].Message.Split('\n')[0] : "Tap the bell in the app to read them."
        });
    }
}
