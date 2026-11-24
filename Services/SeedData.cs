using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Starting data written to local JSON on first launch.
/// In DEBUG builds (running from Visual Studio) it also creates dummy accounts so you can sign in quickly:
/// one Dean per school, one Chairperson and one Faculty per department, plus five READS Scholars (each with the school they study in
/// and a different school assigned to monitor) and one IMS Personnel.
/// Release builds get no accounts at all; everyone registers.
/// </summary>
public static class SeedData
{
    /// <summary>Password shared by every dummy account (debug builds only).</summary>
    public const string DummyPassword = "Fm@2026!";

    // Kept so older code that still reads SeedData.Departments keeps compiling; the catalog is the real source.
    public static IReadOnlyList<string> Departments => AcademicCatalog.AllDepartments;

    private const string Ims = AcademicCatalog.ImsOffice;

    /// <summary>How many dummy READS Scholar accounts to create (rds01, rds02, ...).</summary>
    private const int ReadsScholarCount = 5;

    // Short codes used in dummy account codes (used in internal record IDs). Matched against the department name.
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
        (string Hash, string Salt) pw, string assignedSchool = "", string assignedDept = "", string ownSchool = "") => new()
    {
        Id = id, SisId = sis, Email = $"{sis}@fms.test", FullName = name, School = school, Department = dept, Role = role,
        AssignedSchool = assignedSchool, AssignedDepartment = assignedDept, OwnSchool = ownSchool, PasswordHash = pw.Hash, Salt = pw.Salt
    };
#endif

    public static List<UserAccount> Users()
    {
#if DEBUG
        // Every dummy account shares one password, so hash it once instead of once per account.
        var pw = PasswordHasher.Hash(DummyPassword);
        var units = Units();

        var users = new List<UserAccount>
        {
            User("U-IMS", "ims01", "IMS Personnel (dummy)", "", Ims, UserRole.ImsPersonnel, pw),
        };

        // A READS Scholar monitors a school other than the one they study in (to avoid favoritism toward their own faculty):
        // rds01-rds03 study in the School of Business and Management and monitor the School of Engineering and Architecture;
        // rds04-rds05 study in the School of Engineering and Architecture and monitor the School of Arts and Sciences.
        var sea = AcademicCatalog.Schools[0].Name;
        var sbm = AcademicCatalog.Schools.First(s => s.Name.Contains("Business")).Name;
        var sas = AcademicCatalog.Schools.First(s => s.Name.Contains("Arts and Sciences")).Name;
        for (var i = 1; i <= ReadsScholarCount; i++)
        {
            var own = i <= 3 ? sbm : sea;
            var assigned = i <= 3 ? sea : sas;
            users.Add(User($"U-READS-{i}", $"rds{i:00}", $"READS Scholar {i} (dummy)", "", Ims, UserRole.ReadsScholar, pw,
                assignedSchool: assigned, ownSchool: own));
        }

        // Sign-in IDs are exactly 5 characters: dean1.., chr01.., fac01.. (in catalog order).
        var schoolList = units.Select(u => (u.School, u.SchoolCode)).Distinct().ToList();
        for (var i = 0; i < schoolList.Count; i++)
        {
            var school = schoolList[i];
            users.Add(User($"U-DEAN-{school.SchoolCode}", $"dean{i + 1}",
                $"Dean {school.SchoolCode.ToUpperInvariant()} (dummy)", school.School, "", UserRole.Dean, pw));
        }

        for (var i = 0; i < units.Count; i++)
        {
            var u = units[i];
            users.Add(User($"U-CHAIR-{u.Code}", $"chr{i + 1:00}", $"Chairperson {u.Code.ToUpperInvariant()} (dummy)",
                u.School, u.Department, UserRole.Chairperson, pw));
            users.Add(User($"U-FAC-{u.Code}", $"fac{i + 1:00}", $"Faculty {u.Code.ToUpperInvariant()} (dummy)",
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
            Email = $"fac{Units().IndexOf(u) + 1:00}@fms.test"
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

        // ---- Your own subjects: add one line per subject, (Code, Title, Units, Department). ----
        // Department must match the name in AcademicCatalog.cs exactly, for example "Department of Electrical Engineering".
        // Seed data is written only on first launch, so uninstall the app and reinstall it to see changes.
        var extra = new (string Code, string Title, int Units, string Dept)[]
        {
            // ("EE201", "Circuit Analysis", 3, "Department of Electrical Engineering"),
            // ("CE305", "Structural Design", 3, "Department of Civil Engineering"),
        };
        foreach (var e in extra)
            subjects.Add(new Subject { Code = e.Code, Title = e.Title, Units = e.Units, Department = e.Dept });

        return subjects;
    }

    /// <summary>Main Campus classrooms from <see cref="CampusLayout"/>: RoomsPerFloor rooms on every floor of every enabled building.</summary>
    public static List<Classroom> Classrooms()
    {
        var rooms = new List<Classroom>();
        foreach (var building in CampusLayout.MainCampus.Where(b => b.Enabled))
            foreach (var floor in building.Floors)
                for (var n = 1; n <= CampusLayout.RoomsPerFloor; n++)
                    rooms.Add(new Classroom
                    {
                        Id = $"R-{rooms.Count + 1:000}", Campus = AcademicCatalog.DefaultCampus,
                        // Rooms belong to no school: schools share buildings and floors, so the requesting Faculty's own Dean approves a room change.
                        Building = building.Code, Floor = floor.Floor, RoomNo = CampusLayout.RoomName(floor.Floor, n)
                    });
        return rooms;
    }

    // ---- Sample schedule data (debug builds only) so every screen has something to show -------------------------------
    // Every department's dummy Faculty teaches its "<CODE>101" subject on Mondays.
    // School of Engineering and Architecture classes use the San Agustin basement rooms (B-1, then B-2);
    // the other schools' sample classes use the San Agustin 2nd floor (rooms there are shared between schools).

    public static List<ClassSchedule> Schedules()
    {
#if DEBUG
        var rooms = Classrooms();
        var subjects = Subjects();
        var units = Units();
        var engineering = AcademicCatalog.Schools[0].Name;
        var basement = rooms.Where(r => r.Building == "SA" && r.Floor.StartsWith("B-")).ToList();   // B-1 then B-2 (20 rooms)
        var shared = rooms.Where(r => r.Building == "SA" && r.Floor == "2").ToList();               // 2nd floor (10 rooms)
        var list = new List<ClassSchedule>();
        var engIndex = 0; var otherIndex = 0;
        for (var i = 0; i < units.Count; i++)
        {
            var u = units[i];
            var subject = subjects.First(s => s.Department == u.Department);
            var isEng = u.School == engineering;
            var pool = isEng ? basement : shared;
            var n = isEng ? engIndex++ : otherIndex++;
            var room = pool[n % pool.Count];
            var startMinutes = 8 * 60 + (n / pool.Count) * 90;       // first pool-full of classes at 8:00, the next at 9:30
            list.Add(new ClassSchedule
            {
                Id = $"S-seed-{u.Code}", FacultyUserId = $"U-FAC-{u.Code}", FacultyName = $"Faculty {u.Code.ToUpperInvariant()} (dummy)",
                School = u.School, Department = u.Department, SubjectCode = subject.Code, SubjectTitle = subject.Title,
                ClassroomId = room.Id, Day = DayOfWeek.Monday,
                Start = $"{startMinutes / 60:00}:{startMinutes % 60:00}", End = $"{(startMinutes + 90) / 60:00}:{(startMinutes + 90) % 60:00}"
            });
        }
        return list;
#else
        return new();
#endif
    }

    // READS Scholars enter their own vacant times now, and monitor the classes of their assigned school inside them,
    // so there are no duty assignments to seed. (The duties file and DutyAssignment record are kept only so older data still loads.)
    public static List<DutyAssignment> Duties() => new();

    // Sample vacant times (debug builds only), Mondays. rds01 and rds02 monitor the Engineering classes; rds04 monitors Arts and Sciences.
    public static List<VacantSlot> Vacancies()
    {
#if DEBUG
        return new()
        {
            new() { Id = "V-seed-1", ReadsUserId = "U-READS-1", Day = DayOfWeek.Monday, Start = "07:30", End = "12:00" },
            new() { Id = "V-seed-2", ReadsUserId = "U-READS-2", Day = DayOfWeek.Monday, Start = "07:30", End = "12:00" },
            new() { Id = "V-seed-3", ReadsUserId = "U-READS-4", Day = DayOfWeek.Monday, Start = "07:30", End = "12:00" },
        };
#else
        return new();
#endif
    }

    // Sample class assignments (debug builds only): IMS gives each READS Scholar the classes of the school they monitor that lie inside
    // their vacant times. rds01 and rds02 both monitor Engineering, so they share its classes (a class belongs to one Scholar only).
    public static List<CheckAssignment> CheckAssignments()
    {
#if DEBUG
        var rooms = Classrooms().ToDictionary(r => r.Id);
        var vacancies = Vacancies();
        var sea = AcademicCatalog.Schools[0].Name;
        var sas = AcademicCatalog.Schools.First(x => x.Name.Contains("Arts and Sciences")).Name;
        var list = new List<CheckAssignment>();
        var n = 0;
        foreach (var sch in Schedules())
        {
            string? reads = null;
            if (sch.School == sea) reads = n++ % 2 == 0 ? "U-READS-1" : "U-READS-2";
            else if (sch.School == sas) reads = "U-READS-4";
            if (reads is null || !rooms.TryGetValue(sch.ClassroomId, out var room)) continue;
            if (!vacancies.Any(v => v.ReadsUserId == reads && v.Day == sch.Day && ScheduleRules.Contains(v.Start, v.End, sch.Start, sch.End))) continue;
            list.Add(new CheckAssignment
            {
                Id = $"CA-seed-{list.Count + 1}", ReadsUserId = reads, ScheduleId = sch.Id, Building = room.Building, Floor = room.Floor
            });
        }
        return list;
#else
        return new();
#endif
    }
}
