using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Dean: every department of the Dean's school with its Chairperson(s) and Faculty (read-only).</summary>
public partial class SchoolDirectoryViewModel : ObservableObject
{
    private readonly DirectoryService _directory;
    public SchoolDirectoryViewModel(DirectoryService directory) => _directory = directory;

    public ObservableCollection<DepartmentGroup> Departments { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotValid))]
    private bool _isValid;

    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _summary = "";

    public bool IsNotValid => !IsValid;

    public async Task LoadAsync()
    {
        var result = await _directory.LoadForCurrentDeanAsync();

        Departments.Clear();
        foreach (var group in result.Departments) Departments.Add(group);

        var chairs = result.Departments.Sum(g => g.Chairpersons.Count);
        var faculty = result.Departments.Sum(g => g.Faculty.Count);
        School = result.School;
        Summary = $"{result.Departments.Count} departments · {chairs} chairpersons · {faculty} faculty";
        IsValid = result.IsValid;
    }
}
