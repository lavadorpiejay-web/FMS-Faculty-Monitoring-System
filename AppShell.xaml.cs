using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring;

public partial class AppShell : Shell
{
    private readonly SessionService _session;
    private readonly NotificationService _notifications;
    private readonly LocalAlertService _alerts;

    public AppShell(SessionService session, NotificationService notifications, LocalAlertService alerts)
    {
        InitializeComponent();
        _session = session;
        _notifications = notifications;
        _alerts = alerts;
        Routing.RegisterRoute("Register", typeof(Views.RegisterPage));
        Routing.RegisterRoute("VerifyEmail", typeof(Views.VerifyEmailPage));
        Routing.RegisterRoute("ForgotPassword", typeof(Views.ForgotPasswordPage));
        Routing.RegisterRoute("Notifications", typeof(Views.NotificationsPage));
        Navigating += OnNavigating;
        Navigated += (_, _) => AttachBell();
    }

    // Every signed-in page gets the bell (with its unread count) in the top bar.
    private void AttachBell()
    {
        if (!_session.IsAuthenticated) return;
        if (CurrentPage is not ContentPage page || page is Views.NotificationsPage) return;
        if (GetTitleView(page) is not null) return;
        SetTitleView(page, new Controls.NotificationTitleView(page, _notifications));
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
        _ = _alerts.SyncAsync();                      // phone reminders and any new-notification alert
    }

    public async Task SignOutAsync()
    {
        _alerts.CancelReminders();                    // before the session ends
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
