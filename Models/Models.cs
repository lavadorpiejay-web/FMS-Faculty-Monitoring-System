namespace FacultyMonitoring.Models;

public enum UserRole { Faculty, ReadsScholar, ImsPersonnel, Chairperson, Dean }

public sealed record UserAccount
{
    public string Id { get; init; } = "";
    public string SisId { get; init; } = "";
    public string FullName { get; init; } = "";
    public string School { get; init; } = "";       // empty for IMS Office staff
    public string Department { get; init; } = "";   // empty for Deans; "IMS Office" for READS Scholar / IMS Personnel
    // For READS Scholars only: the school and department IMS Personnel assigned them to view.
    public string AssignedSchool { get; init; } = "";
    public string AssignedDepartment { get; init; } = "";
    public UserRole Role { get; init; }
    public bool IsActive { get; init; } = true;
    public string PasswordHash { get; init; } = "";
    public string Salt { get; init; } = "";
}

public sealed record FacultyMember
{
    public string Id { get; init; } = "";
    public string? UserId { get; init; }
    public string EmployeeNo { get; init; } = "";
    public string FullName { get; init; } = "";
    public string School { get; init; } = "";
    public string Department { get; init; } = "";
    public string Email { get; init; } = "";
}

public sealed record Subject
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public int Units { get; init; }
    public string Department { get; init; } = "";
}

public sealed record Classroom
{
    public string Id { get; init; } = "";
    public string Building { get; init; } = "";
    public string RoomNo { get; init; } = "";
    public int Capacity { get; init; }
}

public sealed record CheckResult(string Title, string Detail, bool Passed)
{
    public string Symbol => Passed ? "✓" : "✕";
}

/// <summary>One person shown in the READS Scholar directory (name, role and department only).</summary>
public sealed record DirectoryEntry(string Name, string Detail);

/// <summary>One department with its Chairperson(s) and Faculty (Dean's view of their school).</summary>
public sealed record DepartmentGroup(
    string Department, IReadOnlyList<DirectoryEntry> Chairpersons, IReadOnlyList<DirectoryEntry> Faculty)
{
    public bool NoChairperson => Chairpersons.Count == 0;
    public bool NoFaculty => Faculty.Count == 0;
    public string FacultyCountText => Faculty.Count == 1 ? "1 faculty member" : $"{Faculty.Count} faculty members";
}

/// <summary>One line of the IMS Personnel "current assignments" list.</summary>
public sealed record AssignmentRow(string Name, string Assignment);

public sealed record AuthResult(bool Success, string Message, UserAccount? User);

public sealed record RegistrationRequest(
    string FullName, string SisId, string School, string Department,
    UserRole Role, string Password, string ConfirmPassword);

public sealed record RoleInfo(
    string DisplayName,
    string Tagline,
    string StatusHeading,
    string ReportsTo,
    string Oversees,
    IReadOnlyList<string> Allowed,
    IReadOnlyList<string> Restricted);
