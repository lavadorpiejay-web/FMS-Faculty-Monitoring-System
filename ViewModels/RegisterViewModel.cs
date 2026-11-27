using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly VerificationService _verification;
    public RegisterViewModel(IAuthService auth, VerificationService verification)
    {
        _auth = auth;
        _verification = verification;
    }

    public IReadOnlyList<string> RoleLabels { get; } =
        RolePolicy.AllRoles.Select(r => RolePolicy.Describe(r).DisplayName).ToList();

    /// <summary>Faculty and Chairperson may also pick the Center for Religious Education (an independent department, not under any school). Deans and READS pick a real school.</summary>
    public IReadOnlyList<string> Schools => CurrentRole is UserRole.Faculty or UserRole.Chairperson ? AcademicCatalog.UnitNames : AcademicCatalog.SchoolNames;

    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _sisId = "";

    // Live "already registered" labels under Full name and ID.
    [ObservableProperty] private bool _nameTaken;
    [ObservableProperty] private bool _sisTaken;

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private bool _emailTaken;

    // READS Scholars use a student ID (up to 10 characters); everyone else an employee ID of exactly 5.
    public int IdMaxLength => LocalAuthService.MaxIdLength(CurrentRole);
    public string IdHint => LocalAuthService.IdRule(CurrentRole);

    // Who must approve this account after the email is verified.
    public bool HasApprovalNote => ApprovalNote.Length > 0;
    public string ApprovalNote => LocalAuthService.ApproverOf(CurrentRole ?? UserRole.Dean, SelectedSchool) is { } approver
        ? $"After you confirm your email, your {RolePolicy.Describe(approver).DisplayName} must approve your account before you can sign in."
        : "";

    private CancellationTokenSource? _duplicateCts;

    partial void OnFullNameChanged(string value) => _ = CheckDuplicatesAsync();
    partial void OnSisIdChanged(string value) => _ = CheckDuplicatesAsync();
    partial void OnEmailChanged(string value) => _ = CheckDuplicatesAsync();

    private async Task CheckDuplicatesAsync()
    {
        _duplicateCts?.Cancel();
        var cts = _duplicateCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cts.Token);               // wait until the user pauses typing
            var result = await _auth.CheckDuplicatesAsync(FullName, SisId, Email);
            if (cts.IsCancellationRequested) return;
            NameTaken = result.NameTaken;
            SisTaken = result.SisTaken;
            EmailTaken = result.EmailTaken;
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
    [NotifyPropertyChangedFor(nameof(ApprovalNote))]
    [NotifyPropertyChangedFor(nameof(HasApprovalNote))]
    private string? _selectedSchool;

    public bool HasSchool => SelectedSchool is not null;

    private UserRole? CurrentRole => RolePolicy.FromLabel(SelectedRoleLabel);

    // Faculty, Chairperson and Dean belong to a school; Faculty and Chairperson also to a department.
    // A READS Scholar also picks a school: the school they study in (IMS never assigns them to monitor it).
    public bool NeedsSchool => CurrentRole is UserRole.Faculty or UserRole.Chairperson or UserRole.Dean or UserRole.ReadsScholar;
    public string SchoolCaption => CurrentRole == UserRole.ReadsScholar ? "Your school (the school you study in)"
        : CurrentRole is UserRole.Faculty or UserRole.Chairperson ? "School (or Center for Religious Education)" : "School";
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
        // Only a Dean or a READS Scholar needs a real school: the Center for Religious Education is not one.
        if (CurrentRole is UserRole.Dean or UserRole.ReadsScholar && AcademicCatalog.IsIndependent(SelectedSchool)) SelectedSchool = null;
        _ = CheckOccupiedAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleHint))]
    [NotifyPropertyChangedFor(nameof(HasRoleHint))]
    [NotifyPropertyChangedFor(nameof(NeedsSchool))]
    [NotifyPropertyChangedFor(nameof(Schools))]
    [NotifyPropertyChangedFor(nameof(SchoolCaption))]
    [NotifyPropertyChangedFor(nameof(NeedsDepartment))]
    [NotifyPropertyChangedFor(nameof(IsOfficeRole))]
    [NotifyPropertyChangedFor(nameof(IdMaxLength))]
    [NotifyPropertyChangedFor(nameof(IdHint))]
    [NotifyPropertyChangedFor(nameof(ApprovalNote))]
    [NotifyPropertyChangedFor(nameof(HasApprovalNote))]
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
        FullName = SisId = Email = Password = ConfirmPassword = ErrorMessage = "";
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
                FullName, SisId, Email, NeedsSchool ? SelectedSchool ?? "" : "", NeedsDepartment ? SelectedDepartment ?? "" : "",
                role.Value, Password, ConfirmPassword));

            if (!result.Success) { ErrorMessage = result.Message; return; }

            // The account exists but is not usable until the email is confirmed: send the code now.
            var user = result.User!;
            var send = await _verification.SendRegistrationCodeAsync(user.Id);
            if (send.Simulated)
                await Shell.Current.DisplayAlertAsync("Simulated email (no mail account set up)", send.SimulatedMail, "OK");
            else if (!send.Success)
                await Shell.Current.DisplayAlertAsync("Code not sent", send.Message + "\nYou can ask for a new code on the next screen.", "OK");

            await Shell.Current.GoToAsync($"../VerifyEmail?userId={Uri.EscapeDataString(user.Id)}");
        }
        finally { IsBusy = false; }
    }
}
