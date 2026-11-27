using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>One class of today on the Faculty home page, with the READS' record of it.</summary>
public sealed record AttendanceCardRow(string Title, string Detail, string StatusText, Microsoft.Maui.Graphics.Color StatusColor,
    string State, string Checks, bool HasChecks);

public partial class DashboardViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly AttendanceService _attendance;
    private readonly ClockService _clock;

    public DashboardViewModel(SessionService session, AttendanceService attendance, ClockService clock)
    {
        _session = session;
        _attendance = attendance;
        _clock = clock;
    }

    public ObservableCollection<AttendanceCardRow> TodayRows { get; } = new();

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _roleName = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _todayHeading = "Your attendance today";
    [ObservableProperty] private string _todaySummary = "";
    [ObservableProperty] private bool _hasNoClassesToday;

    public async Task LoadAsync()
    {
        var user = _session.CurrentUser;
        if (user is null) return;

        var info = RolePolicy.Describe(user.Role);
        Greeting = $"Welcome, {user.FullName}";
        RoleName = info.DisplayName;
        Department = AcademicCatalog.UnitLine(user.School, user.Department);

        TodayHeading = $"Your attendance today, {_clock.Now:dddd, MMM d}";
        var classes = await _attendance.GetMyClassesTodayAsync();
        TodayRows.Clear();
        foreach (var c in classes)
        {
            var (text, color) = c.Status switch
            {
                AttendanceStatus.Present => ("Present", Microsoft.Maui.Graphics.Color.FromArgb("#1B6B40")),
                AttendanceStatus.Late => ("Late", Microsoft.Maui.Graphics.Color.FromArgb("#8A6A00")),
                AttendanceStatus.Absent => ("Absent", Microsoft.Maui.Graphics.Color.FromArgb("#B3261E")),
                _ => ("-", Microsoft.Maui.Graphics.Color.FromArgb("#5C6B62"))
            };
            TodayRows.Add(new AttendanceCardRow(c.Subject, $"{c.Time} | {c.Room}", text, color, c.State, c.Checks, c.Checks.Length > 0));
        }
        HasNoClassesToday = classes.Count == 0;

        var present = classes.Count(c => c.Status == AttendanceStatus.Present);
        var late = classes.Count(c => c.Status == AttendanceStatus.Late);
        var absent = classes.Count(c => c.Status == AttendanceStatus.Absent);
        TodaySummary = classes.Count == 0 ? "You have no classes today."
            : $"{classes.Count} class{(classes.Count == 1 ? "" : "es")}: {present} present, {late} late, {absent} absent.";
    }
}
