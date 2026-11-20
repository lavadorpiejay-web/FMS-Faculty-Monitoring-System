using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>One READS Scholar as IMS Personnel see them: the school they monitor and the vacant times they entered.</summary>
public sealed record ScholarDutyRow(string Name, string Assignment, string VacantSummary);

/// <summary>
/// IMS Personnel overview of READS Scholars. READS Scholars enter their own vacant times (see <see cref="VacantTimeService"/>);
/// IMS only looks at them, and assigns the school on the Assign tab.
/// </summary>
public sealed class DutyService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;

    public DutyService(JsonStorageService storage, SessionService session)
    {
        _storage = storage;
        _session = session;
    }

    private static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

    public async Task<List<ScholarDutyRow>> GetOverviewAsync()
    {
        if (_session.Role != UserRole.ImsPersonnel) return new();
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var vacancies = await _storage.LoadAsync<VacantSlot>(JsonStorageService.VacanciesFile);

        return users.Where(u => u.Role == UserRole.ReadsScholar && u.IsUsable).OrderBy(u => u.FullName)
            .Select(u =>
            {
                var slots = vacancies.Where(v => v.ReadsUserId == u.Id).OrderBy(v => DayOrder(v.Day)).ThenBy(v => v.Start)
                    .Select(v => $"{v.Day} {TimeFormat.Range(v.Start, v.End)}").ToList();
                var assignment = u.AssignedSchool.Length == 0
                    ? "Not assigned to a school yet"
                    : $"Monitors: {u.AssignedSchool}";
                if (u.OwnSchool.Length > 0) assignment += $" (studies in {u.OwnSchool})";
                return new ScholarDutyRow(u.FullName, assignment,
                    slots.Count == 0 ? "No vacant times entered yet" : string.Join("\n", slots));
            }).ToList();
    }
}
