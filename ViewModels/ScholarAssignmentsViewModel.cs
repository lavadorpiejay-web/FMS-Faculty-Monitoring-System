using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class ScholarAssignmentsViewModel : ObservableObject
{
    private readonly ScholarAssignmentService _service;
    private Dictionary<string, ScholarInfo> _byLabel = new();

    public ScholarAssignmentsViewModel(ScholarAssignmentService service) => _service = service;

    public ObservableCollection<AssignmentRow> Assignments { get; } = new();
    // The school the Scholar studies in is left out: a Scholar monitors another school, never their own.
    [ObservableProperty] private IReadOnlyList<string> _schools = AcademicCatalog.SchoolNames;

    [ObservableProperty] private IReadOnlyList<string> _scholarLabels = Array.Empty<string>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScholarSelected))]
    private string? _selectedScholarLabel;

    [ObservableProperty] private string? _selectedSchool;
    [ObservableProperty] private string _ownSchoolText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string _successMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoScholars))]
    private bool _hasScholars;

    public bool HasScholarSelected => SelectedScholarLabel is not null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);
    public bool IsNotBusy => !IsBusy;
    public bool HasNoScholars => !HasScholars;

    // Choosing a Scholar shows their current assignment in the pickers.
    partial void OnSelectedScholarLabelChanged(string? value)
    {
        ErrorMessage = "";
        SuccessMessage = "";

        if (value is not null && _byLabel.TryGetValue(value, out var info))
        {
            Schools = AcademicCatalog.SchoolNames.Where(s => !string.Equals(s, info.OwnSchool, StringComparison.OrdinalIgnoreCase)).ToList();
            OwnSchoolText = info.OwnSchool.Length > 0
                ? $"This Scholar studies in the {info.OwnSchool}, so that school is not offered."
                : "";
            SelectedSchool = info.AssignedSchool.Length > 0 ? info.AssignedSchool : null;
        }
        else
        {
            Schools = AcademicCatalog.SchoolNames;
            OwnSchoolText = "";
            SelectedSchool = null;
        }
    }

    public async Task LoadAsync()
    {
        var scholars = await _service.GetScholarsAsync();
        _byLabel = scholars.ToDictionary(s => s.Label);

        var labels = scholars.Select(s => s.Label).ToList();
        if (!ScholarLabels.SequenceEqual(labels)) ScholarLabels = labels;   // keep the picker selection when nothing changed
        HasScholars = labels.Count > 0;

        Assignments.Clear();
        foreach (var s in scholars) Assignments.Add(new AssignmentRow(s.Name, s.Assignment));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        SuccessMessage = "";

        if (SelectedScholarLabel is null || !_byLabel.TryGetValue(SelectedScholarLabel, out var info))
        { ErrorMessage = "Choose a READS Scholar."; return; }
        if (SelectedSchool is null) { ErrorMessage = "Choose a school."; return; }

        try
        {
            IsBusy = true;
            var result = await _service.AssignAsync(info.UserId, SelectedSchool);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            SuccessMessage = result.Message;
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        SuccessMessage = "";

        if (SelectedScholarLabel is null || !_byLabel.TryGetValue(SelectedScholarLabel, out var info))
        { ErrorMessage = "Choose a READS Scholar."; return; }

        try
        {
            IsBusy = true;
            var result = await _service.ClearAsync(info.UserId);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            SelectedSchool = null;
            await LoadAsync();
            SuccessMessage = result.Message;
        }
        finally { IsBusy = false; }
    }
}
