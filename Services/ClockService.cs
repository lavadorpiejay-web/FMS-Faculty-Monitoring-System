namespace FacultyMonitoring.Services;

/// <summary>
/// The app's "now". In debug builds the Attendance screen can set a test time so duty windows and class hours can be tried at any hour.
/// </summary>
public sealed class ClockService
{
    public DateTime? Override { get; set; }
    public DateTime Now => Override ?? DateTime.Now;
}
