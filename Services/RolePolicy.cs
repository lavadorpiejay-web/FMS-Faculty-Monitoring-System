using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Single source of truth for what each role may see.
/// Hierarchy: Dean > Chairperson > Faculty, and IMS Personnel > READS Scholar.
/// </summary>
public static class RolePolicy
{
    public static string HomeRoute(UserRole role) => $"//{role}Home";

    public static IReadOnlyList<UserRole> AllRoles { get; } = Enum.GetValues<UserRole>();

    public static UserRole? FromLabel(string? label) =>
        AllRoles.Cast<UserRole?>().FirstOrDefault(r => Describe(r!.Value).DisplayName == label);

    public static bool IsRouteAllowed(UserRole? role, string location)
    {
        location = location.Split('?')[0];                              // ignore query strings such as VerifyEmail?userId=...
        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries)
                               .Where(s => !s.StartsWith("IMPL_", StringComparison.Ordinal))
                               .ToList();
        if (segments.Count == 0) return true;

        var first = segments[0];
        if (first == "..") return true;                                 // back navigation
        // Only when signed out.
        if (first is "Login" or "Register" or "VerifyEmail" or "ForgotPassword") return role is null;
        return role is not null && first.StartsWith(role.Value.ToString(), StringComparison.Ordinal);
    }

    public static RoleInfo Describe(UserRole role) => role switch
    {
        UserRole.Faculty => new RoleInfo(
            "Faculty", "Teaches classes and requests schedule or room changes.",
            "Account and shell verification",
            ReportsTo: "Chairperson", Oversees: "",
            Allowed: new[] { "View your class schedule", "Request schedule changes (Chairperson approves) and room changes (Dean approves)" },
            Restricted: new[] { "Attendance checking controls", "Managing other users", "Administrative controls" }),

        UserRole.ReadsScholar => new RoleInfo(
            "READS Scholar", "Working student who checks faculty attendance in the monitoring area and duty hours assigned by IMS Personnel.",
            "Infrastructure initialization",
            ReportsTo: "IMS Personnel (when assigned to the IMS Office)", Oversees: "",
            Allowed: new[] { "Duty dashboard: current, upcoming and completed assignments", "Digital attendance logging (Present, Late, Absent) for classes in your monitoring area", "Report attendance irregularities", "Read-only directory of the Dean, Chairperson and Faculty in your assigned department" },
            Restricted: new[] { "Classes outside your assigned area and duty hours", "Managing other users", "Administrative overrides" }),

        UserRole.ImsPersonnel => new RoleInfo(
            "IMS Personnel", "Manages the application and supervises READS Scholars.",
            "Infrastructure initialization",
            ReportsTo: "", Oversees: "The application and READS Scholars assigned to the IMS Office",
            Allowed: new[] { "Encode faculty schedules and classrooms", "Apply approved schedule and room changes", "Assign READS duty schedules and monitoring areas", "Master schedule, duty, attendance and audit oversight", "Assign READS Scholars to a school and department", "Approve new READS Scholar accounts" },
            Restricted: new[] { "Active attendance logging", "Managing Faculty, Chairperson and Dean accounts", "Chairperson and Dean controls", "Administrative overrides on academic units" }),

        UserRole.Chairperson => new RoleInfo(
            "Chairperson", "Department-level infrastructure views for your academic unit.",
            "System initialization check",
            ReportsTo: "Dean", Oversees: "Faculty",
            Allowed: new[] { "Approve or decline Faculty schedule change requests", "Approve new Faculty accounts", "Read-only list of the Faculty in your department" },
            Restricted: new[] { "Checker-level attendance permissions", "Dean-level controls" }),

        _ => new RoleInfo(
            "Dean", "Oversees the departments of your school.",
            "Platform readiness verification",
            ReportsTo: "", Oversees: "Chairpersons and Faculty",
            Allowed: new[] { "Approve or decline classroom change requests for rooms of your school", "Approve new Chairperson accounts", "Read-only list of the Chairpersons and Faculty in each department of your school" },
            Restricted: new[] { "Day-to-day attendance logging" })
    };
}
