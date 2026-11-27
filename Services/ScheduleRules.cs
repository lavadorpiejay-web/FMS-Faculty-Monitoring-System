using System.Globalization;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Shared time, day and monitoring-area rules used by every schedule-related service and screen.</summary>
public static class ScheduleRules
{
    public const string NoChange = "(no change)";
    public const string WholeSchool = "(whole school)";
    public const string AllDepartments = "All departments";

    public static readonly IReadOnlyList<string> DayNames = new List<string>
    { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    public static IReadOnlyList<string> DayChoicesWithNoChange { get; } = new[] { NoChange }.Concat(DayNames).ToList();

    /// <summary>06:00 to 22:00 in 30-minute steps.</summary>
    public static IReadOnlyList<string> Times { get; } = BuildTimes();
    public static IReadOnlyList<string> TimesWithNoChange { get; } = new[] { NoChange }.Concat(BuildTimes()).ToList();

    private static List<string> BuildTimes()
    {
        var list = new List<string>();
        for (var m = 6 * 60; m <= 22 * 60; m += 30) list.Add($"{m / 60:00}:{m % 60:00}");
        return list;
    }

    public static DayOfWeek ParseDay(string name) => Enum.Parse<DayOfWeek>(name);

    /// <summary>Days written as letters, Monday first: "MWF", "TTh", "Sa". Used to show one class that meets on several days as one line.</summary>
    public static string DayLetters(IEnumerable<DayOfWeek> days) => string.Concat(days.Distinct()
        .OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d)
        .Select(d => d switch
        {
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "Th",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "Sa",
            _ => "Su"
        }));

    public static int Minutes(string hhmm)
    {
        var p = hhmm.Split(':');
        return int.Parse(p[0], CultureInfo.InvariantCulture) * 60 + int.Parse(p[1], CultureInfo.InvariantCulture);
    }

    public static string Pretty(string hhmm) =>
        DateTime.Today.AddMinutes(Minutes(hhmm)).ToString("h:mm tt", CultureInfo.InvariantCulture);

    public static string Range(string start, string end) => $"{Pretty(start)} - {Pretty(end)}";

    public static string HHmm(DateTime t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string HHmmOf(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
    public static string DateText(DateTime t) => t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string ClockText(DateTime t) => t.ToString("ddd MMM d, h:mm tt", CultureInfo.InvariantCulture);

    public static bool Overlaps(string s1, string e1, string s2, string e2) =>
        Minutes(s1) < Minutes(e2) && Minutes(s2) < Minutes(e1);

    public static bool Contains(string outerStart, string outerEnd, string start, string end) =>
        Minutes(outerStart) <= Minutes(start) && Minutes(end) <= Minutes(outerEnd);

    public static bool NowWithin(DateTime now, string start, string end)
    {
        var m = Minutes(HHmm(now));
        return Minutes(start) <= m && m < Minutes(end);
    }

    public static string? CheckTimes(string? start, string? end)
    {
        if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end)) return "Choose a start and an end time.";
        return Minutes(end) <= Minutes(start) ? "The end time must be after the start time." : null;
    }

    public static string CampusOf(Classroom room) => room.Campus.Length > 0 ? room.Campus : AcademicCatalog.DefaultCampus;

    public static string RoomLabel(Classroom? room) =>
        room is null ? "Unknown room" : $"{CampusOf(room)} - {room.Building} {room.RoomNo}";

    public static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the class room is inside the duty's monitoring area: campus, then building and floor when they are set.
    /// It looks only at the classroom the schedule is held in, never at who teaches it or which school or department
    /// the teacher belongs to. (The schedule parameter is kept so existing callers do not change.)
    /// </summary>
    public static bool DutyCovers(DutyAssignment duty, ClassSchedule schedule, Classroom? room) =>
        room is not null
        && Same(duty.Campus, CampusOf(room))
        && (duty.Building.Length == 0 || Same(duty.Building, room.Building))
        && (duty.Floor.Length == 0 || Same(duty.Floor, room.Floor));

    public static string AreaText(DutyAssignment d)
    {
        var parts = new List<string>();
        if (d.Building.Length > 0) parts.Add($"Building {d.Building}");
        else if (d.Floor.Length == 0) parts.Add("All buildings");
        if (d.Floor.Length > 0) parts.Add($"Floor {d.Floor}");
        return string.Join(", ", parts);
    }

    public static string DutyText(DutyAssignment d) =>
        $"{d.Day} {Range(d.Start, d.End)} | {d.Campus} | {AreaText(d)}";

    public static string ScheduleLabel(ClassSchedule s) => $"{s.SubjectCode} {s.Day} {Range(s.Start, s.End)}";
}
