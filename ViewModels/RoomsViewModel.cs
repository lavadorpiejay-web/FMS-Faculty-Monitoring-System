using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>IMS Personnel: manage the core classroom records. Rooms are listed in collapsible sections, one per building.</summary>
public partial class RoomsViewModel : PageViewModelBase
{
    private readonly ScheduleService _service;

    public RoomsViewModel(ScheduleService service) => _service = service;

    public ObservableCollection<RowGroup> Buildings { get; } = new();
    public IReadOnlyList<string> Campuses { get; } = AcademicCatalog.Campuses;
    public IReadOnlyList<string> Schools { get; } = AcademicCatalog.SchoolNames;

    [ObservableProperty] private string? _selectedCampus;
    [ObservableProperty] private string? _selectedSchool;
    [ObservableProperty] private string _building = "";
    [ObservableProperty] private string _floor = "";
    [ObservableProperty] private string _roomNo = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRooms))]
    private bool _hasRooms;

    public bool HasNoRooms => !HasRooms;

    // Floors sort as numbers when they are numbers ("2" before "10"), otherwise as text.
    private static (int, string) FloorKey(string floor) =>
        int.TryParse(floor, out var n) ? (n, "") : (int.MaxValue, floor);

    private static int CampusOrder(string campus)
    {
        for (var i = 0; i < AcademicCatalog.Campuses.Count; i++)
            if (string.Equals(AcademicCatalog.Campuses[i], campus, StringComparison.OrdinalIgnoreCase)) return i;
        return int.MaxValue;
    }

    public async Task LoadAsync()
    {
        // Remember which buildings were open so adding a room does not collapse everything.
        var open = Buildings.Where(b => b.IsExpanded).Select(b => b.Key).ToHashSet();
        var rooms = await _service.GetRoomsAsync();

        var groups = rooms
            .GroupBy(r => (Campus: ScheduleRules.CampusOf(r), Building: r.Building.Trim()),
                     new BuildingComparer())
            .OrderBy(g => CampusOrder(g.Key.Campus))
            .ThenBy(g => g.Key.Building, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var key = $"{g.Key.Campus}|{g.Key.Building}".ToLowerInvariant();
                var rows = g.OrderBy(r => FloorKey(r.Floor)).ThenBy(r => r.RoomNo, StringComparer.OrdinalIgnoreCase)
                    .Select(r => new ListRow(r.Id, $"Room {r.RoomNo}",
                        $"Floor {(r.Floor.Length > 0 ? r.Floor : "-")} | {(r.School.Length > 0 ? r.School : "no school set")}"))
                    .ToList();
                return new RowGroup(key, g.Key.Building, g.Key.Campus, rows, open.Contains(key));
            })
            .ToList();

        Buildings.Clear();
        foreach (var g in groups) Buildings.Add(g);
        HasRooms = Buildings.Count > 0;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ClearMessages();
        try
        {
            IsBusy = true;
            var result = await _service.AddRoomAsync(SelectedCampus, SelectedSchool, Building, Floor, RoomNo);
            Show(result);
            if (!result.Success) return;
            RoomNo = "";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    /// <summary>Same campus and building name (ignoring case) belong to one section.</summary>
    private sealed class BuildingComparer : IEqualityComparer<(string Campus, string Building)>
    {
        public bool Equals((string Campus, string Building) a, (string Campus, string Building) b) =>
            string.Equals(a.Campus, b.Campus, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Building, b.Building, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Campus, string Building) k) =>
            HashCode.Combine(k.Campus.ToLowerInvariant(), k.Building.ToLowerInvariant());
    }
}
