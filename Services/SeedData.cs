using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Starting data written to local JSON on first launch (and again after the app is uninstalled and reinstalled).
/// There is NO dummy data here any more. Everything you want to start with goes in the five clearly marked places below:
///   1. MyAccounts   - sign-in accounts (IMS Personnel, READS Scholars, Deans, Chairpersons, Faculty)
///   2. MySubjects   - subjects (code, title, units, department)
///   3. MySchedules  - class schedules (faculty, subject, day, time, room)
///   4. MyVacantTimes - READS Scholars' vacant times (they can also enter these themselves on the Duty tab)
///   5. Classrooms   - generated from CampusLayout.cs (edit that file to change buildings, floors and rooms)
/// Schools, departments and campuses are in AcademicCatalog.cs.
/// Anything not listed here can be entered inside the app (registration, IMS Schedules tab, IMS Assign tab, READS Duty tab).
/// </summary>
public static class SeedData
{
    /// <summary>No longer used (there are no dummy accounts). Kept so older code that mentions it still compiles.</summary>
    public const string DummyPassword = "";

    // Kept so older code that still reads SeedData.Departments keeps compiling; the catalog is the real source.
    public static IReadOnlyList<string> Departments => AcademicCatalog.AllDepartments;

    private const string Ims = AcademicCatalog.ImsOffice;

    // =====================================================================================================================
    // 1. ACCOUNTS: add one line per account.
    //    (Id, Name, Role, School, Department, Password, Email, AssignedSchool, OwnSchool)
    //    - Id: exactly 5 characters, no spaces (READS Scholars: 5 to 10). This is what they type to sign in.
    //    - Role: UserRole.ImsPersonnel, UserRole.ReadsScholar, UserRole.Dean, UserRole.Chairperson or UserRole.Faculty.
    //    - School / Department: must match AcademicCatalog.cs exactly. Dean: School only (Department = ""). IMS and READS: both "".
    //    - Password: at least 8 characters with an uppercase letter, a lowercase letter, a number and a symbol.
    //    - AssignedSchool / OwnSchool: only for READS Scholars (the school they monitor, and the school they study in; they differ).
    //    Accounts added here are already email-verified and approved.
    //    Example lines (remove the // to use):
    //      ("ims01", "Your Name",   UserRole.ImsPersonnel, "", "", "Pass@word1", "ims@school.edu", "", ""),
    //      ("dean1", "Dean Name",   UserRole.Dean, "School of Engineering and Architecture", "", "Pass@word1", "dean@school.edu", "", ""),
    //      ("chr01", "Chair Name",  UserRole.Chairperson, "School of Engineering and Architecture", "Department of Computer Engineering", "Pass@word1", "chair@school.edu", "", ""),
    //      ("fac01", "Faculty Name", UserRole.Faculty, "School of Engineering and Architecture", "Department of Computer Engineering", "Pass@word1", "fac@school.edu", "", ""),
    //      ("rds01", "Student Name", UserRole.ReadsScholar, "", "", "Pass@word1", "rds@school.edu", "School of Engineering and Architecture", "School of Business and Management"),
    // =====================================================================================================================
    private static readonly (string Id, string Name, UserRole Role, string School, string Department, string Password,
                             string Email, string AssignedSchool, string OwnSchool)[] MyAccounts =
    {
    };

    // =====================================================================================================================
    // 2. SUBJECTS: add one line per subject, (Code, Title, Units, Department).
    //    Units decide the weekly pattern on the IMS Schedules tab (3 units = MWF 1 hour, 2 units = TTh 1 hour 30 minutes).
    //    Department must match AcademicCatalog.cs exactly. A subject belongs to one department.
    //    Example lines:
    //      ("CPE101", "Computer Engineering Fundamentals", 3, "Department of Computer Engineering"),
    // =====================================================================================================================
    private static readonly (string Code, string Title, int Units, string Department)[] MySubjects =
    {
    };

    // =====================================================================================================================
    // 3. SCHEDULES: add one line per class meeting, (FacultyId, SubjectCode, Day, Start, End, Building, Floor, Room).
    //    - FacultyId: the Id of the account in MyAccounts (Faculty, Chairperson or Dean).
    //    - SubjectCode: a Code from MySubjects. Day: DayOfWeek.Monday ... DayOfWeek.Sunday.
    //    - Start / End: 24-hour "HH:mm" in 30-minute steps from 06:00 to 22:00, for example "08:00", "09:30".
    //    - Building / Floor / Room: as in CampusLayout.cs, for example "SA", "B-1", "B-1-01".
    //    A line that does not match an account, subject or room is skipped (see the Output window in Visual Studio).
    //    Example line:
    //      ("fac01", "CPE101", DayOfWeek.Monday, "08:00", "09:00", "SA", "B-1", "B-1-01"),
    // =====================================================================================================================
    private static readonly (string FacultyId, string SubjectCode, DayOfWeek Day, string Start, string End,
                             string Building, string Floor, string Room)[] MySchedules =
    {
    };

    // =====================================================================================================================
    // 4. VACANT TIMES (optional): (ReadsId, Day, Start, End). READS Scholars can enter their own on the Duty tab instead.
    //    Example line:
    //      ("rds01", DayOfWeek.Monday, "07:30", "12:00"),
    // =====================================================================================================================
    private static readonly (string ReadsId, DayOfWeek Day, string Start, string End)[] MyVacantTimes =
    {
    };

    // ---------------------------------------------------------------------------------------------------------------------
    // Code below turns the lists above into records. You normally do not need to edit it.
    // ---------------------------------------------------------------------------------------------------------------------

    private static string UserId(string id) => $"U-{id}";

    public static List<UserAccount> Users()
    {
        var users = new List<UserAccount>();
        foreach (var a in MyAccounts)
        {
            var pw = PasswordHasher.Hash(a.Password);
            users.Add(new UserAccount
            {
                Id = UserId(a.Id), SisId = a.Id, Email = a.Email, FullName = a.Name, School = a.School,
                Department = (a.Role == UserRole.ImsPersonnel || a.Role == UserRole.ReadsScholar) ? Ims : a.Department, Role = a.Role,
                AssignedSchool = a.AssignedSchool, OwnSchool = a.OwnSchool, PasswordHash = pw.Hash, Salt = pw.Salt
            });
        }
        return users;
    }

    // Faculty records are normally created when a Faculty member registers. Faculty accounts listed in MyAccounts get theirs here.
    public static List<FacultyMember> Faculty() =>
        MyAccounts.Where(a => a.Role == UserRole.Faculty).Select(a => new FacultyMember
        {
            Id = $"F-{a.Id}", UserId = UserId(a.Id), EmployeeNo = a.Id, FullName = a.Name,
            School = a.School, Department = a.Department, Email = a.Email
        }).ToList();

    public static List<Subject> Subjects() =>
        MySubjects.Select(s => new Subject { Code = s.Code, Title = s.Title, Units = s.Units, Department = s.Department }).ToList();

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

    public static List<ClassSchedule> Schedules()
    {
        var rooms = Classrooms();
        var subjects = Subjects();
        var list = new List<ClassSchedule>();
        foreach (var s in MySchedules)
        {
            var who = MyAccounts.FirstOrDefault(a => a.Id == s.FacultyId);
            var subject = subjects.FirstOrDefault(x => x.Code == s.SubjectCode);
            var room = rooms.FirstOrDefault(r => r.Building == s.Building && r.Floor == s.Floor && r.RoomNo == s.Room);
            if (who.Id is null || subject is null || room is null)
            {
                System.Diagnostics.Debug.WriteLine($"SeedData: schedule line skipped ({s.FacultyId}, {s.SubjectCode}, {s.Building} {s.Floor} {s.Room}): faculty, subject or room not found.");
                continue;
            }
            list.Add(new ClassSchedule
            {
                Id = $"S-my-{list.Count + 1}", FacultyUserId = UserId(who.Id), FacultyName = who.Name,
                School = who.School, Department = subject.Department, SubjectCode = subject.Code, SubjectTitle = subject.Title,
                ClassroomId = room.Id, Day = s.Day, Start = s.Start, End = s.End
            });
        }
        return list;
    }

    // Duty assignments are no longer used (READS Scholars enter vacant times; IMS assigns classes on the Assign tab).
    // The duties file and DutyAssignment record are kept only so older data still loads.
    public static List<DutyAssignment> Duties() => new();

    public static List<VacantSlot> Vacancies() =>
        MyVacantTimes.Select((v, i) => new VacantSlot { Id = $"V-my-{i + 1}", ReadsUserId = UserId(v.ReadsId), Day = v.Day, Start = v.Start, End = v.End }).ToList();

    // Which classes each READS Scholar checks is decided by IMS Personnel on the Assign tab, so nothing is seeded.
    public static List<CheckAssignment> CheckAssignments() => new();
}
