namespace FacultyMonitoring;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    // Resolve the shell lazily so App resources (colors/styles) are loaded before it is built.
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        // Coming back to the app: refresh the reminders and alert about notifications that arrived meanwhile.
        window.Resumed += (_, _) => _ = _services.GetRequiredService<Services.LocalAlertService>().SyncAsync();
        return window;
    }
}
