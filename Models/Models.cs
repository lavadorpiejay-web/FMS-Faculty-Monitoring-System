namespace FacultyMonitoring.Models;

public enum UserRole { Faculty, ReadsScholar, ImsPersonnel, Chairperson, Dean }

/// <summary>Faculty are approved by their Chairperson, Chairpersons by their Dean. Everyone else is approved automatically.</summary>
public enum ApprovalStatus { Approved, Pending, Declined }

/// <summary>Why a sign-in or registration step did not succeed, so the screens can offer the right next step.</summary>
public enum AuthIssue { None, Invalid, EmailNotVerified, PendingApproval, Declined, Inactive, Locked }

public sealed record UserAccount
{
    public string Id { get; init; } = "";
    public string SisId { get; init; } = "";
    public string FullName { get; init; } = "";
    public string School { get; init; } = "";       // empty for IMS Office staff
    public string Department { get; init; } = "";   // empty for Deans; "IMS Office" for READS Scholar / IMS Personnel
    // For READS Scholars only: the school IMS Personnel assigned them to monitor (and view in the Directory).
    public string AssignedSchool { get; init; } = "";
    public string AssignedDepartment { get; init; } = "";   // no longer used: a READS Scholar is assigned to a whole school
    // For READS Scholars only: the school they study in. IMS Personnel cannot assign them to monitor their own school.
    public string OwnSchool { get; init; } = "";
    public UserRole Role { get; init; }
    public bool IsActive { get; init; } = true;
    public string Email { get; init; } = "";
    // Defaults keep older saved accounts and the debug dummy accounts working: verified and approved.
    public bool EmailVerified { get; init; } = true;
    public ApprovalStatus Status { get; init; } = ApprovalStatus.Approved;
    /// <summary>True when the account may appear in directories and sign in: active, email verified and approved.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUsable => IsActive && EmailVerified && Status == ApprovalStatus.Approved;
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
    public string Campus { get; init; } = "";      // empty on older saved rooms = the default campus
    public string School { get; init; } = "";      // no longer used (rooms belong to no school); kept so older saved rooms still load
    public string Building { get; init; } = "";
    public string Floor { get; init; } = "";
    public string RoomNo { get; init; } = "";
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

public sealed record AuthResult(bool Success, string Message, UserAccount? User, AuthIssue Issue = AuthIssue.None);

public enum CodePurpose { VerifyEmail, ResetPassword }

/// <summary>A one-time email code. Only a hash of the code is stored.</summary>
public sealed record VerificationCode
{
    public string UserId { get; init; } = "";
    public CodePurpose Purpose { get; init; }
    public string CodeHash { get; init; } = "";
    public DateTime SentUtc { get; init; }
    public DateTime ExpiresUtc { get; init; }
    public int Attempts { get; init; }
}

/// <summary>One account waiting for approval, shown to the Chairperson (Faculty) or Dean (Chairpersons).</summary>
public sealed record PendingApproval(string UserId, string Name, string Id, string Email, string Detail);

public sealed record RegistrationRequest(
    string FullName, string SisId, string Email, string School, string Department,
    UserRole Role, string Password, string ConfirmPassword);

public sealed record RoleInfo(
    string DisplayName,
    string Tagline,
    string StatusHeading,
    string ReportsTo,
    string Oversees,
    IReadOnlyList<string> Allowed,
    IReadOnlyList<string> Restricted);
