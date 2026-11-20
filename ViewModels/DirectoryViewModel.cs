using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class DirectoryViewModel : ObservableObject
{
    private readonly DirectoryService _directory;
    public DirectoryViewModel(DirectoryService directory) => _directory = directory;

    public ObservableCollection<DirectoryEntry> Deans { get; } = new();
    // Replaced in one assignment so the page lays out once.
    [ObservableProperty] private IReadOnlyList<DirectoryDepartment> _departments = Array.Empty<DirectoryDepartment>();

    // The "Departments" heading opens and closes the whole list.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepartmentsArrow))]
    private bool _departmentsOpen;

    public string DepartmentsArrow => DepartmentsOpen ? "▼" : "▶";
    public string DepartmentsCountText => Departments.Count == 1 ? "1 department" : $"{Departments.Count} departments";

    [RelayCommand]
    private void ToggleDepartments() => DepartmentsOpen = !DepartmentsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotAssigned))]
    private bool _isAssigned;

    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private bool _deansEmpty;

    public bool IsNotAssigned => !IsAssigned;

    public async Task LoadAsync()
    {
        var result = await _directory.LoadForCurrentScholarAsync();

        Fill(Deans, result.Deans);

        // Keep the chairpersons the scholar already opened open after a reload.
        var open = Departments.SelectMany(d => d.Chairpersons).Where(c => c.IsExpanded)
                              .Select(c => c.Name).ToHashSet();
        Departments = result.Departments.Select(g => new DirectoryDepartment(
            g.Department,
            g.Chairpersons.Select(c => new ChairpersonNode(c, g.Faculty, open.Contains(c.Name))).ToList(),
            g.Faculty)).ToList();
        OnPropertyChanged(nameof(DepartmentsCountText));

        School = result.School;
        Department = result.Department;
        DeansEmpty = result.Deans.Count == 0;
        IsAssigned = result.IsAssigned;
    }

    private static void Fill(ObservableCollection<DirectoryEntry> target, IEnumerable<DirectoryEntry> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
