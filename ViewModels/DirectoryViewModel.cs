using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class DirectoryViewModel : ObservableObject
{
    private readonly DirectoryService _directory;
    public DirectoryViewModel(DirectoryService directory) => _directory = directory;

    public ObservableCollection<DirectoryEntry> Deans { get; } = new();
    public ObservableCollection<DirectoryEntry> Chairpersons { get; } = new();
    public ObservableCollection<DirectoryEntry> Faculty { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotAssigned))]
    private bool _isAssigned;

    [ObservableProperty] private string _school = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private bool _deansEmpty;
    [ObservableProperty] private bool _chairpersonsEmpty;
    [ObservableProperty] private bool _facultyEmpty;

    public bool IsNotAssigned => !IsAssigned;

    public async Task LoadAsync()
    {
        var result = await _directory.LoadForCurrentScholarAsync();

        Fill(Deans, result.Deans);
        Fill(Chairpersons, result.Chairpersons);
        Fill(Faculty, result.Faculty);

        School = result.School;
        Department = result.Department;
        DeansEmpty = result.Deans.Count == 0;
        ChairpersonsEmpty = result.Chairpersons.Count == 0;
        FacultyEmpty = result.Faculty.Count == 0;
        IsAssigned = result.IsAssigned;
    }

    private static void Fill(ObservableCollection<DirectoryEntry> target, IEnumerable<DirectoryEntry> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
