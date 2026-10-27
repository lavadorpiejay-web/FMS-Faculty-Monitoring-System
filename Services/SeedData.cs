using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Starting data written to local JSON on first launch (no user accounts).</summary>
public static class SeedData
{
    // Kept so older code that still reads SeedData.Departments keeps compiling; the catalog is the real source.
    public static IReadOnlyList<string> Departments => AcademicCatalog.AllDepartments;

    private const string CpE = "Department of Computer Engineering";
    private const string EE = "Department of Electrical Engineering";

    // No pre-made accounts: every account is created through the Register screen.
    public static List<UserAccount> Users() => new();

    // Faculty records are created automatically when a Faculty member registers.
    public static List<FacultyMember> Faculty() => new();

    public static List<Subject> Subjects() => new()
    {
        new() { Code = "CPE101", Title = "Introduction to Computer Engineering", Units = 3, Department = CpE },
        new() { Code = "CPE204", Title = "Microprocessor Systems", Units = 3, Department = CpE },
        new() { Code = "EE110", Title = "Electric Circuits 1", Units = 3, Department = EE },
    };

    public static List<Classroom> Classrooms() => new()
    {
        new() { Id = "R-001", Building = "Main", RoomNo = "101", Capacity = 40 },
        new() { Id = "R-002", Building = "Main", RoomNo = "202", Capacity = 35 },
        new() { Id = "R-003", Building = "Science", RoomNo = "Lab 1", Capacity = 30 },
    };
}
