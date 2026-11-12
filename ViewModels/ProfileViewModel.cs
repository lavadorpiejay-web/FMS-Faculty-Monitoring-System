using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly SessionService _session;
    public ProfileViewModel(SessionService session) => _session = session;

    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _sisId = "";
    [ObservableProperty] private string _roleName = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _reportsTo = "";
    [ObservableProperty] private string _oversees = "";

    public bool HasReportsTo => ReportsTo.Length > 0;
    public bool HasOversees => Oversees.Length > 0;

    partial void OnReportsToChanged(string value) => OnPropertyChanged(nameof(HasReportsTo));
    partial void OnOverseesChanged(string value) => OnPropertyChanged(nameof(HasOversees));

    public void Load()
    {
        var u = _session.CurrentUser;
        if (u is null) return;
        var info = RolePolicy.Describe(u.Role);
        FullName = u.FullName;
        SisId = u.SisId;
        RoleName = info.DisplayName;
        Department = string.Join(" · ", new[] { u.School, u.Department }.Where(x => !string.IsNullOrEmpty(x)));
        ReportsTo = info.ReportsTo;
        Oversees = info.Oversees;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        // Signs out straight away, with no confirmation pop-up.
        await ((AppShell)Shell.Current).SignOutAsync();
    }
}
