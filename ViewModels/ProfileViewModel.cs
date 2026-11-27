using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class ProfileViewModel : PageViewModelBase
{
    private readonly SessionService _session;
    private readonly ResignationService _resignations;

    public ProfileViewModel(SessionService session, ResignationService resignations)
    {
        _session = session;
        _resignations = resignations;
    }

    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _sisId = "";
    [ObservableProperty] private string _roleName = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _reportsTo = "";
    [ObservableProperty] private string _oversees = "";

    // ---- Resignation card (Faculty, Chairperson, Dean, READS)
    [ObservableProperty] private bool _canResign;
    [ObservableProperty] private bool _showResignForm;
    [ObservableProperty] private bool _hasOpenResignation;
    [ObservableProperty] private bool _hasDeclined;
    [ObservableProperty] private string _resignationReason = "";
    [ObservableProperty] private string _resignationRoute = "";
    [ObservableProperty] private string _resignationTrail = "";
    [ObservableProperty] private string _resignationDeclined = "";
    public int ReasonMaxLength => ResignationService.ReasonMaxLength;

    public bool HasReportsTo => ReportsTo.Length > 0;
    public bool HasOversees => Oversees.Length > 0;

    partial void OnReportsToChanged(string value) => OnPropertyChanged(nameof(HasReportsTo));
    partial void OnOverseesChanged(string value) => OnPropertyChanged(nameof(HasOversees));

    public async Task LoadAsync()
    {
        var u = _session.CurrentUser;
        if (u is null) return;
        var info = RolePolicy.Describe(u.Role);
        FullName = u.FullName;
        SisId = u.SisId;
        RoleName = info.DisplayName;
        Department = AcademicCatalog.UnitLine(u.School, u.Department);
        ReportsTo = info.ReportsTo;
        Oversees = info.Oversees;

        await LoadResignationAsync();
    }

    private async Task LoadResignationAsync()
    {
        var u = _session.CurrentUser;
        CanResign = u is not null && ResignationService.CanResign(u.Role);
        if (!CanResign || u is null) { ShowResignForm = HasOpenResignation = HasDeclined = false; return; }

        ResignationRoute = ResignationService.RouteText(u.Role);
        var mine = await _resignations.GetMineAsync();

        HasOpenResignation = mine is { IsOpen: true };
        ResignationTrail = mine is null ? "" : ResignationService.Trail(mine);
        HasDeclined = mine is { Stage: ResignationStage.Declined };
        ResignationDeclined = mine is { Stage: ResignationStage.Declined }
            ? $"Your last resignation was declined by the {mine.DeclinedByRole} {mine.DeclinedByName}: {mine.DeclineReason}"
            : "";
        ShowResignForm = !HasOpenResignation;
    }

    [RelayCommand]
    private async Task SubmitResignationAsync()
    {
        if (IsBusy) return;
        var yes = await Shell.Current.DisplayAlertAsync("Submit resignation",
            "Send your resignation? You can withdraw it until IMS Personnel make it official.", "Send", "Cancel");
        if (!yes) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            var result = await _resignations.SubmitAsync(ResignationReason);
            Show(result);
            if (result.Success) ResignationReason = "";
            await LoadResignationAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task WithdrawResignationAsync()
    {
        if (IsBusy) return;
        var yes = await Shell.Current.DisplayAlertAsync("Withdraw resignation",
            "Withdraw your resignation?", "Withdraw", "Cancel");
        if (!yes) return;
        try
        {
            IsBusy = true;
            ClearMessages();
            Show(await _resignations.WithdrawAsync());
            await LoadResignationAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        // Signs out straight away, with no confirmation pop-up.
        await ((AppShell)Shell.Current).SignOutAsync();
    }
}
