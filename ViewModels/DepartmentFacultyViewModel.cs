using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Chairperson: the Faculty of the Chairperson's own department (read-only).</summary>
public partial class DepartmentFacultyViewModel : ObservableObject
{
    private readonly DirectoryService _directory;
    public DepartmentFacultyViewModel(DirectoryService directory) => _directory = directory;

    public ObservableCollection<DirectoryEntry> Faculty { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotValid))]
    private bool _isValid;

    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _facultyEmpty;

    public bool IsNotValid => !IsValid;

    public async Task LoadAsync()
    {
        var result = await _directory.LoadForCurrentChairpersonAsync();

        Faculty.Clear();
        foreach (var f in result.Faculty) Faculty.Add(f);

        School = result.School;
        Department = result.Department;
        FacultyEmpty = result.Faculty.Count == 0;
        Summary = result.Faculty.Count == 1 ? "1 faculty member" : $"{result.Faculty.Count} faculty members";
        IsValid = result.IsValid;
    }
}
