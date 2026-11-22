using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// One READS Scholar as IMS Personnel see them: the school they monitor and the vacant times they entered.
/// The name is a toggle: tapping it opens or closes that Scholar's weekly vacant-time timetable.
/// </summary>
public partial class ScholarDutyRow : ObservableObject
{
    public ScholarDutyRow(string userId, string name, string assignment, string vacantSummary, IReadOnlyList<TimetableBlock> blocks, bool expanded = false)
    {
        UserId = userId;
        Name = name;
        Assignment = assignment;
        VacantSummary = vacantSummary;
        Blocks = blocks;
        _isExpanded = expanded;
    }

    /// <summary>Stable identity so the open/closed state survives a reload.</summary>
    public string UserId { get; }
    public string Name { get; }
    public string Assignment { get; }
    public string VacantSummary { get; }
    public IReadOnlyList<TimetableBlock> Blocks { get; }
    public bool HasBlocks => Blocks.Count > 0;
    public bool HasNoBlocks => Blocks.Count == 0;
    public string CountText => Blocks.Count == 0 ? "No vacant times yet" : Blocks.Count == 1 ? "1 vacant time" : $"{Blocks.Count} vacant times";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Arrow))]
    private bool _isExpanded;

    public string Arrow => IsExpanded ? "▼" : "▶";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

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
                    .Select(v => new TimetableBlock(v.Id, v.Day, v.Start, v.End, "Vacant", TimeFormat.Range(v.Start, v.End), "", "Vacant")).ToList();
                var assignment = u.AssignedSchool.Length == 0
                    ? "Not assigned to a school yet"
                    : $"Monitors: {u.AssignedSchool}";
                if (u.OwnSchool.Length > 0) assignment += $" (studies in {u.OwnSchool})";
                return new ScholarDutyRow(u.Id, u.FullName, assignment, "No vacant times entered yet", slots);
            }).ToList();
    }
}
