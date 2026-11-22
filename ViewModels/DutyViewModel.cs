using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// IMS Personnel Duty tab: read-only overview of every READS Scholar, the school they monitor and the vacant times they entered.
/// READS Scholars enter their own vacant times; IMS assigns the school on the Assign tab.
/// </summary>
public partial class DutyViewModel : ObservableObject
{
    private readonly DutyService _service;

    public DutyViewModel(DutyService service) => _service = service;

    public ObservableCollection<ScholarDutyRow> Scholars { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScholars))]
    private bool _hasNone;

    public bool HasScholars => !HasNone;

    public async Task LoadAsync()
    {
        // Keep the Scholars the IMS Personnel already opened open after a reload.
        var open = Scholars.Where(x => x.IsExpanded).Select(x => x.UserId).ToHashSet();
        var overview = await _service.GetOverviewAsync();
        Scholars.Clear();
        foreach (var s in overview)
            Scholars.Add(open.Contains(s.UserId) ? new ScholarDutyRow(s.UserId, s.Name, s.Assignment, s.VacantSummary, s.Blocks, true) : s);
        HasNone = Scholars.Count == 0;
    }
}
