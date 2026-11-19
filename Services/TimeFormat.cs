using System.Globalization;

namespace FacultyMonitoring.Services;

/// <summary>
/// Times are STORED as 24-hour "HH:mm" text (so sorting and overlap checks keep working)
/// and SHOWN to people in 12-hour form ("8:00 AM"). Use these helpers at every screen boundary.
/// </summary>
public static class TimeFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>"13:30" becomes "1:30 PM". Text that is not a time is returned unchanged.</summary>
    public static string To12h(string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm)) return "";
        return DateTime.TryParseExact(hhmm.Trim(), "HH:mm", Inv, DateTimeStyles.None, out var t)
            ? t.ToString("h:mm tt", Inv)
            : hhmm;
    }

    /// <summary>"1:30 PM" becomes "13:30". A value already in "HH:mm" passes through; null when it is not a time.</summary>
    public static string? To24h(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (DateTime.TryParseExact(text, "h:mm tt", Inv, DateTimeStyles.None, out var t12)) return t12.ToString("HH:mm", Inv);
        if (DateTime.TryParseExact(text, "HH:mm", Inv, DateTimeStyles.None, out var t24)) return t24.ToString("HH:mm", Inv);
        return null;
    }

    /// <summary>"08:00","09:30" becomes "8:00 AM - 9:30 AM".</summary>
    public static string Range(string? start, string? end) => $"{To12h(start)} - {To12h(end)}";
}
