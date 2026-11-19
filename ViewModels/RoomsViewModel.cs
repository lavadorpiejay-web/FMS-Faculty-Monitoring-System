using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>IMS Personnel: manage the core classroom records.</summary>
public partial class RoomsViewModel : PageViewModelBase
{
    private readonly ScheduleService _service;

    public RoomsViewModel(ScheduleService service) => _service = service;

    public ObservableCollection<ListRow> Rows { get; } = new();
    public IReadOnlyList<string> Campuses { get; } = AcademicCatalog.Campuses;
    public IReadOnlyList<string> Schools { get; } = AcademicCatalog.SchoolNames;

    [ObservableProperty] private string? _selectedCampus;
    [ObservableProperty] private string? _selectedSchool;
    [ObservableProperty] private string _building = "";
    [ObservableProperty] private string _floor = "";
    [ObservableProperty] private string _roomNo = "";
    [ObservableProperty] private string _capacity = "";

    public async Task LoadAsync()
    {
        Rows.Clear();
        foreach (var r in await _service.GetRoomsAsync())
            Rows.Add(new ListRow(r.Id, ScheduleRules.RoomLabel(r),
                $"Floor {(r.Floor.Length > 0 ? r.Floor : "-")} | capacity {r.Capacity} | {(r.School.Length > 0 ? r.School : "no school set")}"));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        try
        {
            IsBusy = true;
            var result = await _service.AddRoomAsync(SelectedCampus, SelectedSchool, Building, Floor, RoomNo, Capacity);
            Show(result);
            if (!result.Success) return;
            RoomNo = "";
            Capacity = "";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }
}
