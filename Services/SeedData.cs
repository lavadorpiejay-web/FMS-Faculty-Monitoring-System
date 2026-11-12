using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Starting data written to local JSON on first launch.
/// In DEBUG builds (running from Visual Studio) it also creates dummy accounts so you can sign in quickly:
/// one Dean per school, one Chairperson and one Faculty per department, plus one READS Scholar and one IMS Personnel.
/// Release builds get no accounts at all; everyone registers.
/// </summary>
public static class SeedData
{
    /// <summary>Password shared by every dummy account (debug builds only).</summary>
    public const string DummyPassword = "Fm@2026!";

    // Kept so older code that still reads SeedData.Departments keeps compiling; the catalog is the real source.
    public static IReadOnlyList<string> Departments => AcademicCatalog.AllDepartments;

    private const string Ims = AcademicCatalog.ImsOffice;

    // Short codes used in dummy SIS IDs (chair.cpe, faculty.cpe, ...). Matched against the department name.
    private static readonly (string Key, string Code)[] DepartmentCodes =
    {
        ("Electrical", "ee"), ("Computer", "cpe"), ("Civil", "ce"), ("Industrial", "ie"),
        ("Mechanical", "me"), ("Architecture", "arch"), ("Electronics", "ece"),
    };

    /// <summary>One department of the catalog with its short code.</summary>
    private sealed record DeptUnit(string School, string SchoolCode, string Department, string Code);

    private static List<DeptUnit> Units()
    {
        var units = new List<DeptUnit>();
        var used = new HashSet<string>();
        for (var s = 0; s < AcademicCatalog.Schools.Count; s++)
        {
            var school = AcademicCatalog.Schools[s];
            // "School of Engineering and Architecture" -> "sea"
            var schoolCode = new string(school.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => char.IsUpper(w[0])).Select(w => char.ToLowerInvariant(w[0])).ToArray());
            if (schoolCode.Length == 0) schoolCode = $"s{s + 1}";

            for (var d = 0; d < school.Departments.Count; d++)
            {
                var dept = school.Departments[d];
                var code = DepartmentCodes.FirstOrDefault(c => dept.Contains(c.Key)).Code ?? $"s{s + 1}d{d + 1}";
                if (!used.Add(code)) code = $"{code}{s + 1}";   // keep every code unique
                units.Add(new DeptUnit(school.Name, schoolCode, dept, code));
            }
        }
        return units;
    }

#if DEBUG
    private static UserAccount User(string id, string sis, string name, string school, string dept, UserRole role,
        (string Hash, string Salt) pw, string assignedSchool = "", string assignedDept = "") => new()
    {
        Id = id, SisId = sis, FullName = name, School = school, Department = dept, Role = role,
        AssignedSchool = assignedSchool, AssignedDepartment = assignedDept, PasswordHash = pw.Hash, Salt = pw.Salt
    };
#endif

    public static List<UserAccount> Users()
    {
#if DEBUG
        // Every dummy account shares one password, so hash it once instead of once per account.
        var pw = PasswordHasher.Hash(DummyPassword);
        var units = Units();
        var first = units.FirstOrDefault(u => u.Code == "cpe") ?? units.FirstOrDefault();

        var users = new List<UserAccount>
        {
            User("U-IMS", "ims01", "IMS Personnel (dummy)", "", Ims, UserRole.ImsPersonnel, pw),
            // Already assigned, so the READS Directory works right away. Re-assign it on the IMS "Assign" tab.
            User("U-READS", "reads01", "READS Scholar (dummy)", "", Ims, UserRole.ReadsScholar, pw,
                 first?.School ?? "", first?.Department ?? ""),
        };

        foreach (var school in units.Select(u => (u.School, u.SchoolCode)).Distinct())
            users.Add(User($"U-DEAN-{school.SchoolCode}", $"dean.{school.SchoolCode}",
                $"Dean {school.SchoolCode.ToUpperInvariant()} (dummy)", school.School, "", UserRole.Dean, pw));

        foreach (var u in units)
        {
            users.Add(User($"U-CHAIR-{u.Code}", $"chair.{u.Code}", $"Chairperson {u.Code.ToUpperInvariant()} (dummy)",
                u.School, u.Department, UserRole.Chairperson, pw));
            users.Add(User($"U-FAC-{u.Code}", $"faculty.{u.Code}", $"Faculty {u.Code.ToUpperInvariant()} (dummy)",
                u.School, u.Department, UserRole.Faculty, pw));
        }
        return users;
#else
        return new();   // Release builds: no pre-made accounts; everyone registers.
#endif
    }

    // Faculty records are normally created when a Faculty member registers. Dummy Faculty accounts get theirs here.
    public static List<FacultyMember> Faculty()
    {
#if DEBUG
        return Units().Select(u => new FacultyMember
        {
            Id = $"F-{u.Code}", UserId = $"U-FAC-{u.Code}", EmployeeNo = $"EMP-{u.Code.ToUpperInvariant()}",
            FullName = $"Faculty {u.Code.ToUpperInvariant()} (dummy)", School = u.School, Department = u.Department,
            Email = $"faculty.{u.Code}@example.edu"
        }).ToList();
#else
        return new();
#endif
    }

    // One sample subject per department (two for Computer Engineering) so each Chairperson's "DeptUnit mapping" check passes.
    public static List<Subject> Subjects()
    {
        var subjects = Units().Select(u => new Subject
        {
            Code = $"{u.Code.ToUpperInvariant()}101",
            Title = "Introduction to " + u.Department.Replace("Department of ", ""),
            Units = 3, Department = u.Department
        }).ToList();

        var cpe = Units().FirstOrDefault(u => u.Code == "cpe");
        if (cpe is not null)
            subjects.Add(new Subject { Code = "CPE204", Title = "Microprocessor Systems", Units = 3, Department = cpe.Department });
        return subjects;
    }

    public static List<Classroom> Classrooms() => new()
    {
        new() { Id = "R-001", Building = "Main", RoomNo = "101", Capacity = 40 },
        new() { Id = "R-002", Building = "Main", RoomNo = "202", Capacity = 35 },
        new() { Id = "R-003", Building = "Science", RoomNo = "Lab 1", Capacity = 30 },
    };
}
