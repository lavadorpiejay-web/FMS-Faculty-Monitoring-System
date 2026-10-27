using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring;

public partial class AppShell : Shell
{
    private readonly SessionService _session;

    public AppShell(SessionService session)
    {
        InitializeComponent();
        _session = session;
        Routing.RegisterRoute("Register", typeof(Views.RegisterPage));
        Navigating += OnNavigating;
    }

    private BaseShellItem TabsFor(UserRole role) => role switch
    {
        UserRole.Faculty => FacultyTabs,
        UserRole.ReadsScholar => ReadsScholarTabs,
        UserRole.ImsPersonnel => ImsPersonnelTabs,
        UserRole.Chairperson => ChairpersonTabs,
        _ => DeanTabs
    };

    public async Task EnterRoleAsync(UserRole role)
    {
        TabsFor(role).IsVisible = true;               // show only this role's area
        await GoToAsync(RolePolicy.HomeRoute(role));
        LoginItem.IsVisible = false;
    }

    public async Task SignOutAsync()
    {
        _session.End();
        LoginItem.IsVisible = true;
        await GoToAsync("//Login");
        foreach (var r in Enum.GetValues<UserRole>())
            TabsFor(r).IsVisible = false;
    }

    // Second layer of role enforcement: block any navigation outside the signed-in role's area.
    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var location = e.Target?.Location?.OriginalString ?? string.Empty;
        if (!RolePolicy.IsRouteAllowed(_session.Role, location))
            e.Cancel();
    }
}
