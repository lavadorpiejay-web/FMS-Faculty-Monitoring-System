using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly SessionService _session;
    public DashboardViewModel(SessionService session) => _session = session;

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _roleName = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _tagline = "";
    [ObservableProperty] private string _reportsTo = "";
    [ObservableProperty] private string _oversees = "";
    [ObservableProperty] private IReadOnlyList<string> _allowed = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _restricted = Array.Empty<string>();

    public bool HasReportsTo => ReportsTo.Length > 0;
    public bool HasOversees => Oversees.Length > 0;

    partial void OnReportsToChanged(string value) => OnPropertyChanged(nameof(HasReportsTo));
    partial void OnOverseesChanged(string value) => OnPropertyChanged(nameof(HasOversees));

    public void Load()
    {
        var user = _session.CurrentUser;
        if (user is null) return;

        var info = RolePolicy.Describe(user.Role);
        Greeting = $"Welcome, {user.FullName}";
        RoleName = info.DisplayName;
        Department = AcademicCatalog.UnitLine(user.School, user.Department);
        Tagline = info.Tagline;
        ReportsTo = info.ReportsTo;
        Oversees = info.Oversees;
        Allowed = info.Allowed;
        Restricted = info.Restricted;
    }
}
