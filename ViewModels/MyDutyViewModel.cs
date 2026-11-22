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
    private List<VacantSlot> _slots = new();

    public MyDutyViewModel(VacantTimeService service)
    {
        _service = service;
        foreach (var name in ScheduleRules.DayNames) Days.Add(new DayChoice(name));
    }

    public ObservableCollection<DayChoice> Days { get; } = new();
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times.Select(TimeFormat.To12h).ToList();

    /// <summary>The weekly timetable of the vacant times. Replaced in one assignment so it redraws once.</summary>
    [ObservableProperty] private IReadOnlyList<TimetableBlock> _timetable = Array.Empty<TimetableBlock>();

    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlots))]
    private bool _hasNoSlots = true;

    public bool HasSlots => !HasNoSlots;

    public async Task LoadAsync()
    {
        _slots = await _service.GetMineAsync();
        Timetable = _slots
            .Select(v => new TimetableBlock(v.Id, v.Day, v.Start, v.End, "Vacant", TimeFormat.Range(v.Start, v.End), "", "Vacant"))
            .ToList();
        HasNoSlots = Timetable.Count == 0;
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

    private static string Describe(VacantSlot v) => $"{v.Day} {TimeFormat.Range(v.Start, v.End)}";

    /// <summary>
    /// Tapping a block asks what to delete: only that block, every vacant time on that day, every vacant time with that same
    /// start and end (any day), all of them, or exactly the ones the Scholar ticks.
    /// </summary>
    [RelayCommand]
    private async Task RemoveAsync(TimetableBlock? block)
    {
        if (block is null || IsBusy) return;
        var tapped = _slots.FirstOrDefault(s => s.Id == block.Id);
        if (tapped is null) return;

        var scopes = new List<RemovePrompt.Scope> { new($"Only this one ({Describe(tapped)})", new[] { tapped.Id }) };
        void Add(string label, IEnumerable<VacantSlot> items)
        {
            var found = items.Select(s => s.Id).ToList();
            if (found.Count > 1) scopes.Add(new($"{label} ({found.Count})", found));   // a scope of one is the same as "only this one"
        }
        Add($"All on {tapped.Day}", _slots.Where(s => s.Day == tapped.Day));
        Add($"All at {TimeFormat.Range(tapped.Start, tapped.End)} (any day)", _slots.Where(s => s.Start == tapped.Start && s.End == tapped.End));
        Add("All my vacant times", _slots);

        var candidates = _slots.Select(s => new ListRow(s.Id, Describe(s), "")).ToList();
        var ids = await RemovePrompt.AskAsync("Remove vacant time", "vacant time", scopes, candidates);
        if (ids is null) return;

        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.RemoveManyAsync(ids));
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
