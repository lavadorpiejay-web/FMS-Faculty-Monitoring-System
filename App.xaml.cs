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
        => new Window(_services.GetRequiredService<AppShell>());
}
