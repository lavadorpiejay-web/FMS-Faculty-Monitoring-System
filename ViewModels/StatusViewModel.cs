using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class StatusViewModel : ObservableObject
{
    private readonly ReadinessService _readiness;
    private readonly SessionService _session;

    public StatusViewModel(ReadinessService readiness, SessionService session)
    {
        _readiness = readiness;
        _session = session;
    }

    public ObservableCollection<CheckResult> Checks { get; } = new();

    [ObservableProperty] private string _heading = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _allPassed;
    [ObservableProperty] private bool _isRunning;

    [RelayCommand]
    public async Task RunChecksAsync()
    {
        var user = _session.CurrentUser;
        if (user is null || IsRunning) return;

        try
        {
            IsRunning = true;
            Heading = RolePolicy.Describe(user.Role).StatusHeading;

            var results = await _readiness.RunAsync(user);
            Checks.Clear();
            foreach (var c in results) Checks.Add(c);

            var passed = results.Count(c => c.Passed);
            AllPassed = passed == results.Count;
            Summary = AllPassed
                ? $"All {results.Count} checks passed"
                : $"{passed} of {results.Count} checks passed";
        }
        finally { IsRunning = false; }
    }
}
