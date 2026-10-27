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
        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries)
                               .Where(s => !s.StartsWith("IMPL_", StringComparison.Ordinal))
                               .ToList();
        if (segments.Count == 0) return true;

        var first = segments[0];
        if (first == "..") return true;                                 // back navigation
        if (first == "Login" || first == "Register") return role is null; // only when signed out
        return role is not null && first.StartsWith(role.Value.ToString(), StringComparison.Ordinal);
    }

    public static RoleInfo Describe(UserRole role) => role switch
    {
        UserRole.Faculty => new RoleInfo(
            "Faculty", "Verify your account, layout and local data connection.",
            "Account and shell verification",
            ReportsTo: "Chairperson", Oversees: "",
            Allowed: new[] { "Teaching-related interfaces", "Account active-status check", "Local data connection check" },
            Restricted: new[] { "Attendance checking controls", "Managing other users", "Administrative controls" }),

        UserRole.ReadsScholar => new RoleInfo(
            "READS Scholar", "Working student assigned to a university office; checks faculty attendance (active logging is not yet enabled in this build).",
            "Infrastructure initialization",
            ReportsTo: "IMS Personnel (when assigned to the IMS Office)", Oversees: "",
            Allowed: new[] { "Foundation framework for attendance checking", "Local JSON storage initialization check", "Read-only directory of the Dean, Chairperson and Faculty in your assigned department" },
            Restricted: new[] { "Active attendance logging", "Managing other users", "Administrative overrides" }),

        UserRole.ImsPersonnel => new RoleInfo(
            "IMS Personnel", "Manages the application and supervises READS Scholars.",
            "Infrastructure initialization",
            ReportsTo: "", Oversees: "The application and READS Scholars assigned to the IMS Office",
            Allowed: new[] { "Application infrastructure", "Local JSON storage initialization check", "Oversight of READS Scholars in the IMS Office", "Assign READS Scholars to a school and department" },
            Restricted: new[] { "Chairperson and Dean controls", "Administrative overrides on academic units" }),

        UserRole.Chairperson => new RoleInfo(
            "Chairperson", "Department-level infrastructure views for your academic unit.",
            "System initialization check",
            ReportsTo: "Dean", Oversees: "Faculty",
            Allowed: new[] { "Chairperson-level infrastructure views", "Department data mapping verification", "Read-only list of the Faculty in your department" },
            Restricted: new[] { "Checker-level attendance permissions", "Dean-level controls" }),

        _ => new RoleInfo(
            "Dean", "Executive-level view of platform deployment and readiness.",
            "Platform readiness verification",
            ReportsTo: "", Oversees: "Chairpersons and Faculty",
            Allowed: new[] { "Executive-level parameters", "Security and Android compatibility verification", "Read-only list of the Chairpersons and Faculty in each department of your school" },
            Restricted: new[] { "Day-to-day attendance logging" })
    };
}
