using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    public RegisterViewModel(IAuthService auth) => _auth = auth;

    public IReadOnlyList<string> RoleLabels { get; } =
        RolePolicy.AllRoles.Select(r => RolePolicy.Describe(r).DisplayName).ToList();

    public IReadOnlyList<string> Schools { get; } = AcademicCatalog.SchoolNames;

    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _sisId = "";

    // Live "already registered" labels under Full name and ID.
    [ObservableProperty] private bool _nameTaken;
    [ObservableProperty] private bool _sisTaken;

    public int MaxSisIdLength => LocalAuthService.MaxSisIdLength;

    private CancellationTokenSource? _duplicateCts;

    partial void OnFullNameChanged(string value) => _ = CheckDuplicatesAsync();
    partial void OnSisIdChanged(string value) => _ = CheckDuplicatesAsync();

    private async Task CheckDuplicatesAsync()
    {
        _duplicateCts?.Cancel();
        var cts = _duplicateCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cts.Token);               // wait until the user pauses typing
            var result = await _auth.CheckDuplicatesAsync(FullName, SisId);
            if (cts.IsCancellationRequested) return;
            NameTaken = result.NameTaken;
            SisTaken = result.SisTaken;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* the label is only a hint; Register still checks again */ }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchText))]
    [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
    [NotifyPropertyChangedFor(nameof(MatchText))]
    private string _password = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchText))]
    [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
    [NotifyPropertyChangedFor(nameof(MatchText))]
    private string _confirmPassword = "";

    // Each password box has its own Show/Hide switch.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHideText))]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmShowHideText))]
    private bool _isConfirmPasswordHidden = true;

    public string ShowHideText => IsPasswordHidden ? "Show" : "Hide";
    public string ConfirmShowHideText => IsConfirmPasswordHidden ? "Show" : "Hide";

    // Live check under "Confirm password".
    public bool HasMatchText => ConfirmPassword.Length > 0;
    public bool PasswordsMatch => Password == ConfirmPassword;
    public string MatchText => PasswordsMatch ? "Passwords match" : "Passwords do not match";

    [RelayCommand]
    private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private void ToggleConfirmPassword() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden;

    [ObservableProperty] private string? _selectedDepartment;

    // Departments shown in the picker: only those of the chosen school.
    [ObservableProperty] private IReadOnlyList<string> _departments = Array.Empty<string>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSchool))]
    private string? _selectedSchool;

    public bool HasSchool => SelectedSchool is not null;

    private UserRole? CurrentRole => RolePolicy.FromLabel(SelectedRoleLabel);

    // Faculty, Chairperson and Dean belong to a school; Faculty and Chairperson also to a department.
    public bool NeedsSchool => CurrentRole is UserRole.Faculty or UserRole.Chairperson or UserRole.Dean;
    public bool NeedsDepartment => CurrentRole is UserRole.Faculty or UserRole.Chairperson;
    // READS Scholar and IMS Personnel are placed in the IMS Office automatically.
    public bool IsOfficeRole => CurrentRole is UserRole.ReadsScholar or UserRole.ImsPersonnel;

    // Live warning when the Chairperson (per department) or Dean (per school) seat is already taken.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOccupiedWarning))]
    private string _occupiedWarning = "";
    public bool HasOccupiedWarning => OccupiedWarning.Length > 0;

    public string PasswordRule => LocalAuthService.PasswordRule;

    private async Task CheckOccupiedAsync()
    {
        var role = CurrentRole;
        if (role is null) { OccupiedWarning = ""; return; }
        try
        {
            OccupiedWarning = await _auth.CheckRoleOccupiedAsync(role.Value,
                NeedsSchool ? SelectedSchool : null, NeedsDepartment ? SelectedDepartment : null);
        }
        catch (Exception) { OccupiedWarning = ""; }
    }

    partial void OnSelectedDepartmentChanged(string? value) => _ = CheckOccupiedAsync();

    partial void OnSelectedSchoolChanged(string? value)
    {
        SelectedDepartment = null;
        Departments = AcademicCatalog.DepartmentsOf(value);
        _ = CheckOccupiedAsync();
    }

    partial void OnSelectedRoleLabelChanged(string? value)
    {
        if (!NeedsSchool) SelectedSchool = null;          // also clears the department
        else if (!NeedsDepartment) SelectedDepartment = null;
        _ = CheckOccupiedAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleHint))]
    [NotifyPropertyChangedFor(nameof(HasRoleHint))]
    [NotifyPropertyChangedFor(nameof(NeedsSchool))]
    [NotifyPropertyChangedFor(nameof(NeedsDepartment))]
    [NotifyPropertyChangedFor(nameof(IsOfficeRole))]
    private string? _selectedRoleLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsNotBusy => !IsBusy;
    public bool HasRoleHint => RoleHint.Length > 0;

    public string RoleHint
    {
        get
        {
            var role = RolePolicy.FromLabel(SelectedRoleLabel);
            if (role is null) return "";
            var info = RolePolicy.Describe(role.Value);
            var parts = new List<string> { info.Tagline };
            if (info.ReportsTo.Length > 0) parts.Add($"Reports to: {info.ReportsTo}.");
            if (info.Oversees.Length > 0) parts.Add($"Oversees: {info.Oversees}.");
            return string.Join(" ", parts);
        }
    }

    public void Reset()
    {
        FullName = SisId = Password = ConfirmPassword = ErrorMessage = "";
        SelectedSchool = null;
        SelectedDepartment = null;
        SelectedRoleLabel = null;
        IsPasswordHidden = true;
        IsConfirmPasswordHidden = true;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";

        var role = RolePolicy.FromLabel(SelectedRoleLabel);
        if (role is null) { ErrorMessage = "Choose your role."; return; }
        if (NeedsSchool && SelectedSchool is null) { ErrorMessage = "Choose your school."; return; }
        if (NeedsDepartment && SelectedDepartment is null) { ErrorMessage = "Choose your department."; return; }
        await CheckOccupiedAsync();
        if (HasOccupiedWarning) { ErrorMessage = OccupiedWarning; return; }

        try
        {
            IsBusy = true;
            var result = await _auth.RegisterAsync(new RegistrationRequest(
                FullName, SisId, NeedsSchool ? SelectedSchool ?? "" : "", NeedsDepartment ? SelectedDepartment ?? "" : "",
                role.Value, Password, ConfirmPassword));

            if (!result.Success) { ErrorMessage = result.Message; return; }

            await Shell.Current.DisplayAlert("Account created",
                $"You can now sign in with ID \"{SisId.Trim()}\".", "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally { IsBusy = false; }
    }
}
