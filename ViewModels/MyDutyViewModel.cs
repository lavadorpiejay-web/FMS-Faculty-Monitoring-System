using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// READS Scholar's own Duty tab: enter vacant (no-class) times for several days at once, and see them in a table.
/// The vacant times are the Scholar's duty hours.
/// </summary>
public partial class MyDutyViewModel : PageViewModelBase
{
    private readonly VacantTimeService _service;

    public MyDutyViewModel(VacantTimeService service)
    {
        _service = service;
        foreach (var name in ScheduleRules.DayNames) Days.Add(new DayChoice(name));
    }

    public ObservableCollection<DayChoice> Days { get; } = new();
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times.Select(TimeFormat.To12h).ToList();

    /// <summary>The table: Title = day, Detail = from, Extra = to.</summary>
    public ObservableCollection<ListRow> Slots { get; } = new();

    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlots))]
    private bool _hasNoSlots = true;

    public bool HasSlots => !HasNoSlots;

    public async Task LoadAsync()
    {
        Slots.Clear();
        foreach (var v in await _service.GetMineAsync())
            Slots.Add(new ListRow(v.Id, v.Day.ToString(), TimeFormat.To12h(v.Start), TimeFormat.To12h(v.End)));
        HasNoSlots = Slots.Count == 0;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        var days = Days.Where(d => d.IsSelected).Select(d => ScheduleRules.ParseDay(d.Name)).ToList();
        try
        {
            IsBusy = true;
            var result = await _service.AddAsync(days, TimeFormat.To24h(SelectedStart), TimeFormat.To24h(SelectedEnd));
            Show(result);
            if (!result.Success) return;
            foreach (var d in Days) d.IsSelected = false;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        var yes = await Shell.Current.DisplayAlertAsync("Remove vacant time", $"Remove {row.Title}, {row.Detail} - {row.Extra}?", "Remove", "Cancel");
        if (!yes) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.RemoveAsync(row.Id));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
