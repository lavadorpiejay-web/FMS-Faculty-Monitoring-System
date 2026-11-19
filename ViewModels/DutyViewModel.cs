using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// IMS Personnel: record a READS Scholar's vacant times, then assign duty schedules and monitoring areas inside them.
/// Tap Edit on a duty to modify or reassign it; every change is written to the audit trail.
/// </summary>
public partial class DutyViewModel : PageViewModelBase
{
    private readonly DutyService _service;
    private Dictionary<string, ScholarInfo> _scholars = new();
    private Dictionary<string, DutyAssignment> _duties = new();

    public DutyViewModel(DutyService service) => _service = service;

    public ObservableCollection<ListRow> VacantRows { get; } = new();
    public ObservableCollection<ListRow> DutyRows { get; } = new();

    public IReadOnlyList<string> Campuses { get; } = AcademicCatalog.Campuses;
    public IReadOnlyList<string> Days { get; } = ScheduleRules.DayNames;
    public IReadOnlyList<string> Times { get; } = ScheduleRules.Times;

    [ObservableProperty] private IReadOnlyList<string> _scholarLabels = Array.Empty<string>();
    [ObservableProperty] private string? _selectedScholar;
    [ObservableProperty] private string? _selectedCampus;
    [ObservableProperty] private string _building = "";
    [ObservableProperty] private string _floor = "";
    [ObservableProperty] private string? _selectedDay;
    [ObservableProperty] private string? _selectedStart;
    [ObservableProperty] private string? _selectedEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(SaveText))]
    private string? _editingDutyId;

    public bool IsEditing => EditingDutyId is not null;
    public string SaveText => IsEditing ? "Update duty" : "Assign duty";

    partial void OnSelectedScholarChanged(string? value) => _ = RefreshVacanciesAsync();

    private ScholarInfo? CurrentScholar =>
        SelectedScholar is not null && _scholars.TryGetValue(SelectedScholar, out var s) ? s : null;

    private async Task RefreshVacanciesAsync()
    {
        VacantRows.Clear();
        var scholar = CurrentScholar;
        if (scholar is null) return;
        foreach (var v in await _service.GetVacanciesAsync(scholar.UserId))
            VacantRows.Add(new ListRow(v.Id, $"{v.Day} {ScheduleRules.Range(v.Start, v.End)}", ""));
    }

    public async Task LoadAsync()
    {
        var scholars = await _service.GetScholarsAsync();
        _scholars = scholars.ToDictionary(s => s.Label);
        var labels = scholars.Select(s => s.Label).ToList();
        if (!ScholarLabels.SequenceEqual(labels)) ScholarLabels = labels;

        var duties = await _service.GetDutiesAsync();
        _duties = duties.ToDictionary(d => d.Id);
        DutyRows.Clear();
        foreach (var d in duties) DutyRows.Add(new ListRow(d.Id, d.ReadsName, ScheduleRules.DutyText(d)));
        await RefreshVacanciesAsync();
    }

    [RelayCommand]
    private async Task AddVacancyAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        var scholar = CurrentScholar;
        if (scholar is null) { ErrorMessage = "Choose a READS Scholar."; return; }
        if (SelectedDay is null) { ErrorMessage = "Choose a day."; return; }
        try
        {
            IsBusy = true;
            var result = await _service.AddVacancyAsync(scholar.UserId, SelectedDay, SelectedStart, SelectedEnd);
            Show(result);
            if (result.Success) await RefreshVacanciesAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveVacancyAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            var result = await _service.RemoveVacancyAsync(row.Id);
            Show(result);
            if (result.Success) await RefreshVacanciesAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveDutyAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        var scholar = CurrentScholar;
        if (scholar is null) { ErrorMessage = "Choose a READS Scholar."; return; }
        if (SelectedDay is null) { ErrorMessage = "Choose a day."; return; }
        try
        {
            IsBusy = true;
            var result = await _service.SaveDutyAsync(EditingDutyId, scholar.UserId, SelectedCampus,
                Building, Floor, SelectedDay, SelectedStart, SelectedEnd);
            Show(result);
            if (!result.Success) return;
            EditingDutyId = null;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void EditDuty(ListRow? row)
    {
        if (row is null || !_duties.TryGetValue(row.Id, out var d)) return;
        ClearMessages();
        EditingDutyId = d.Id;
        var label = _scholars.FirstOrDefault(s => s.Value.UserId == d.ReadsUserId).Key;
        if (label is not null) SelectedScholar = label;     // change the Scholar to reassign the duty
        SelectedCampus = d.Campus;
        Building = d.Building;
        Floor = d.Floor;
        SelectedDay = d.Day.ToString();
        SelectedStart = d.Start;
        SelectedEnd = d.End;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditingDutyId = null;
        ClearMessages();
    }

    [RelayCommand]
    private async Task RemoveDutyAsync(ListRow? row)
    {
        if (row is null || IsBusy) return;
        var yes = await Shell.Current.DisplayAlertAsync("Remove duty", $"Remove this duty of {row.Title}?", "Remove", "Cancel");
        if (!yes) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _service.RemoveDutyAsync(row.Id));
            if (EditingDutyId == row.Id) EditingDutyId = null;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
