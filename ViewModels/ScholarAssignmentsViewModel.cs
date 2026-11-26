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
    /// <summary>Classes on the chosen floor that can be given to the chosen Scholar (inside their vacant times).</summary>
    public ObservableCollection<ClassPick> Classes { get; } = new();

    public IReadOnlyList<string> Buildings { get; } = CampusLayout.MainCampus.Where(b => b.Enabled).Select(b => CampusLayout.BuildingLabel(b.Code)).ToList();

    [ObservableProperty] private IReadOnlyList<string> _floors = Array.Empty<string>();
    [ObservableProperty] private string? _selectedBuilding;
    [ObservableProperty] private string? _selectedFloor;
    [ObservableProperty] private string _vacantText = "";
    [ObservableProperty] private string _classesMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoClasses))]
    private bool _hasClasses;

    public bool HasNoClasses => !HasClasses;
    public bool HasClassesMessage => !string.IsNullOrEmpty(ClassesMessage);
    partial void OnClassesMessageChanged(string value) => OnPropertyChanged(nameof(HasClassesMessage));
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

    // A READS that already has a school only needs a building and a floor: the school pickers stay hidden unless IMS taps "Change school".
    [ObservableProperty] private bool _isChangingSchool;
    public bool HasAssignedSchool => CurrentScholar()?.AssignedSchool.Length > 0;
    public bool ShowSchoolPicker => HasScholarSelected && (!HasAssignedSchool || IsChangingSchool);
    public bool ShowAssignedSchool => HasScholarSelected && HasAssignedSchool && !IsChangingSchool;
    public string AssignedSchoolText => CurrentScholar() is { AssignedSchool.Length: > 0 } c
        ? $"Assigned school: {c.AssignedSchool}. Choose a building and a floor below."
        : "";
    /// <summary>Save / Remove assignment for the chosen floor are shown once a READS with a school, a building and a floor are chosen.</summary>
    public bool CanAssignFloor => HasAssignedSchool && SelectedBuilding is not null && SelectedFloor is not null;

    private void RefreshSchoolState()
    {
        OnPropertyChanged(nameof(HasAssignedSchool));
        OnPropertyChanged(nameof(ShowSchoolPicker));
        OnPropertyChanged(nameof(ShowAssignedSchool));
        OnPropertyChanged(nameof(AssignedSchoolText));
        OnPropertyChanged(nameof(CanAssignFloor));
    }

    partial void OnIsChangingSchoolChanged(bool value) => RefreshSchoolState();
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);
    public bool IsNotBusy => !IsBusy;
    public bool HasNoScholars => !HasScholars;

    // Choosing a Scholar shows their current assignment in the pickers.
    partial void OnSelectedScholarLabelChanged(string? value)
    {
        ErrorMessage = "";
        SuccessMessage = "";
        _ = ReloadClassesAsync(refreshVacant: true);

        if (value is not null && _byLabel.TryGetValue(value, out var info))
        {
            Schools = AcademicCatalog.SchoolNames.Where(s => !string.Equals(s, info.OwnSchool, StringComparison.OrdinalIgnoreCase)).ToList();
            OwnSchoolText = info.OwnSchool.Length > 0
                ? $"This READS studies in the {info.OwnSchool}, so that school is not offered."
                : "";
            SelectedSchool = info.AssignedSchool.Length > 0 ? info.AssignedSchool : null;
        }
        else
        {
            Schools = AcademicCatalog.SchoolNames;
            OwnSchoolText = "";
            SelectedSchool = null;
        }
        IsChangingSchool = false;
        RefreshSchoolState();
    }

    partial void OnSelectedBuildingChanged(string? value)
    {
        var code = value is null ? "" : CampusLayout.BuildingCode(value);
        var plan = CampusLayout.MainCampus.FirstOrDefault(b => b.Code == code);
        Floors = plan is null ? Array.Empty<string>() : plan.Floors.Select(f => f.Floor).ToList();
        SelectedFloor = null;
        RefreshSchoolState();
        _ = ReloadClassesAsync(refreshVacant: false);
    }

    partial void OnSelectedFloorChanged(string? value)
    {
        RefreshSchoolState();
        _ = ReloadClassesAsync(refreshVacant: false);
    }

    private ScholarInfo? CurrentScholar() =>
        SelectedScholarLabel is not null && _byLabel.TryGetValue(SelectedScholarLabel, out var found) ? found : null;

    private int _reloadVersion;

    /// <summary>Lists the classes IMS can tick for the chosen Scholar, building and floor.</summary>
    private async Task ReloadClassesAsync(bool refreshVacant)
    {
        var version = ++_reloadVersion;   // several changes can fire at once (building then floor): only the newest result is shown
        Classes.Clear();
        HasClasses = false;
        ClassesMessage = "";

        var info = CurrentScholar();
        if (info is null) { VacantText = ""; return; }
        if (refreshVacant)
        {
            var vacant = await _service.GetVacantTextAsync(info.UserId);
            if (version != _reloadVersion) return;
            VacantText = vacant;
        }
        if (SelectedBuilding is null || SelectedFloor is null)
        {
            ClassesMessage = "Choose a building and a floor to see the classes you can give this READS.";
            return;
        }

        var result = await _service.GetClassesAsync(info.UserId, CampusLayout.BuildingCode(SelectedBuilding), SelectedFloor);
        if (version != _reloadVersion) return;
        Classes.Clear();
        foreach (var c in result.Items) Classes.Add(c);
        HasClasses = Classes.Count > 0;
        ClassesMessage = result.Message;
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
        RefreshSchoolState();
    }

    [RelayCommand]
    private void ChangeSchool() => IsChangingSchool = true;

    [RelayCommand]
    private void CancelChangeSchool()
    {
        var info = CurrentScholar();
        SelectedSchool = info is not null && info.AssignedSchool.Length > 0 ? info.AssignedSchool : null;
        IsChangingSchool = false;
    }

    /// <summary>Takes every class of the chosen floor off the chosen READS' list (the same as saving with nothing ticked).</summary>
    [RelayCommand]
    private async Task RemoveFloorAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        SuccessMessage = "";

        var info = CurrentScholar();
        if (info is null) { ErrorMessage = "Choose a READS."; return; }
        if (SelectedBuilding is null || SelectedFloor is null) { ErrorMessage = "Choose a building and a floor."; return; }

        try
        {
            IsBusy = true;
            var none = new List<(string ScheduleId, bool First, bool End)>();
            var result = await _service.SaveClassesAsync(info.UserId, CampusLayout.BuildingCode(SelectedBuilding), SelectedFloor, none);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            await LoadAsync();
            await ReloadClassesAsync(refreshVacant: false);
            SuccessMessage = result.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectAllClasses()
    {
        foreach (var c in Classes)
        {
            if (c.CanStart) c.StartSelected = true;
            if (c.CanEnd) c.EndSelected = true;
        }
    }

    [RelayCommand]
    private void ClearClasses()
    {
        foreach (var c in Classes) { c.StartSelected = false; c.EndSelected = false; }
    }

    [RelayCommand]
    private async Task SaveClassesAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        SuccessMessage = "";

        var info = CurrentScholar();
        if (info is null) { ErrorMessage = "Choose a READS."; return; }
        if (SelectedBuilding is null || SelectedFloor is null) { ErrorMessage = "Choose a building and a floor."; return; }

        try
        {
            IsBusy = true;
            var picks = Classes.Select(c => (c.ScheduleId, c.StartSelected && c.CanStart, c.EndSelected && c.CanEnd)).ToList();
            var result = await _service.SaveClassesAsync(info.UserId, CampusLayout.BuildingCode(SelectedBuilding), SelectedFloor, picks);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            await LoadAsync();                              // refreshes the summary list (classes and floors per Scholar)
            await ReloadClassesAsync(refreshVacant: false); // refreshes the ticks and "already checked by" notes
            SuccessMessage = result.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        SuccessMessage = "";

        if (SelectedScholarLabel is null || !_byLabel.TryGetValue(SelectedScholarLabel, out var info))
        { ErrorMessage = "Choose a READS."; return; }
        if (SelectedSchool is null) { ErrorMessage = "Choose a school."; return; }

        try
        {
            IsBusy = true;
            var result = await _service.AssignAsync(info.UserId, SelectedSchool);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            await LoadAsync();
            IsChangingSchool = false;
            await ReloadClassesAsync(refreshVacant: false);
            SuccessMessage = result.Message;
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
        { ErrorMessage = "Choose a READS."; return; }

        try
        {
            IsBusy = true;
            var result = await _service.ClearAsync(info.UserId);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            SelectedSchool = null;
            await LoadAsync();
            IsChangingSchool = false;
            await ReloadClassesAsync(refreshVacant: false);
            SuccessMessage = result.Message;
        }
        finally { IsBusy = false; }
    }
}
