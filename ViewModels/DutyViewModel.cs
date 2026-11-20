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

    [ObservableProperty] private bool _hasNone;

    public async Task LoadAsync()
    {
        Scholars.Clear();
        foreach (var s in await _service.GetOverviewAsync()) Scholars.Add(s);
        HasNone = Scholars.Count == 0;
    }
}
