using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>READS Scholar: dashboard of current, upcoming and completed monitoring assignments.</summary>
public partial class MyDutyViewModel : PageViewModelBase
{
    private readonly AttendanceService _service;
    private readonly ClockService _clock;

    public MyDutyViewModel(AttendanceService service, ClockService clock)
    {
        _service = service;
        _clock = clock;
    }

    public ObservableCollection<ListRow> Current { get; } = new();
    public ObservableCollection<ListRow> Upcoming { get; } = new();
    public ObservableCollection<ListRow> Completed { get; } = new();

    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private bool _hasNoDuty;

    public async Task LoadAsync()
    {
        ClockText = ScheduleRules.ClockText(_clock.Now);
        Current.Clear(); Upcoming.Clear(); Completed.Clear();
        var duties = await _service.GetMyDutiesAsync();
        foreach (var d in duties)
        {
            var row = new ListRow(d.Id, d.Title, d.Detail);
            switch (d.State)
            {
                case "Current": Current.Add(row); break;
                case "Completed": Completed.Add(row); break;
                default: Upcoming.Add(row); break;
            }
        }
        HasNoDuty = duties.Count == 0;
    }
}
