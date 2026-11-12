using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Dean: every department of the Dean's school with its Chairperson(s) and Faculty (read-only).</summary>
public partial class SchoolDirectoryViewModel : ObservableObject
{
    private readonly DirectoryService _directory;
    public SchoolDirectoryViewModel(DirectoryService directory) => _directory = directory;

    // Replaced in one assignment (not Clear + Add) so the page re-lays out once and the scroll bar does not jump.
    [ObservableProperty] private IReadOnlyList<DepartmentGroup> _departments = Array.Empty<DepartmentGroup>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotValid))]
    private bool _isValid;

    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _summary = "";

    public bool IsNotValid => !IsValid;

    public async Task LoadAsync()
    {
        var result = await _directory.LoadForCurrentDeanAsync();

        var chairs = result.Departments.Sum(g => g.Chairpersons.Count);
        var faculty = result.Departments.Sum(g => g.Faculty.Count);
        School = result.School;
        Summary = $"{result.Departments.Count} departments · {chairs} chairpersons · {faculty} faculty";
        IsValid = result.IsValid;
        Departments = result.Departments;
    }
}
