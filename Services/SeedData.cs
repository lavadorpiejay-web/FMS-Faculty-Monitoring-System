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
        // IMS Personnel
("00000", "Juan Dela Cruz", UserRole.ImsPersonnel, "", "", "Fm@2026!", "ims01@school.edu", "", ""),

// READS Scholar (last two: the school they monitor, then the school they study in)
("2023123451", "Sydney Marzon", UserRole.ReadsScholar, "", "", "Fm@2026!", "rds01@school.edu", "School of Engineering and Architecture", "School of Business and Management"),
("2023123452", "Kyle Roma", UserRole.ReadsScholar, "", "", "Fm@2026!", "rds02@school.edu", "School of Engineering and Architecture", "School of Business and Management"),
("2023123453", "Sheena Lombrino", UserRole.ReadsScholar, "", "", "Fm@2026!", "rds03@school.edu", "School of Engineering and Architecture", "School of Business and Management"),

// Dean (school only)
("10001", "Dennis Trillo", UserRole.Dean, "School of Engineering and Architecture", "", "Fm@2026!", "dean1@school.edu", "", ""),
("10002", "Steve Rogers", UserRole.Dean, "School of Arts and Sciences", "", "Fm@2026!", "dean2@school.edu", "", ""),

// Chairperson (school and department)
("20001", "Sean Pareja", UserRole.Chairperson, "School of Engineering and Architecture", "Department of Computer Engineering", "Fm@2026!", "chr01@school.edu", "", ""),

// Faculty (school and department)
("30001", "Alexis Dacanay", UserRole.Faculty, "School of Engineering and Architecture", "Department of Computer Engineering", "Fm@2026!", "fac01@school.edu", "", ""),
("30002", "Jeren Yacanad", UserRole.Faculty, "School of Engineering and Architecture", "Department of Computer Engineering", "Fm@2026!", "fac02@school.edu", "", ""),
("30003", "Robel Branianola", UserRole.Faculty, "School of Engineering and Architecture", "Department of Computer Engineering", "Fm@2026!", "fac03@school.edu", "", ""),

    };

    // =====================================================================================================================
    // 2. SUBJECTS: add one line per subject, (Code, Title, Units, Department).
    //    Units decide the weekly pattern on the IMS Schedules tab (3 units = MWF 1 hour, 2 units = TTh 1 hour 30 minutes).
    //    Department must match AcademicCatalog.cs exactly. A subject belongs to one department.
    //    Example lines:
    //      ("CPE101", "Computer Engineering Fundamentals", 3, "Department of Computer Engineering")
    // =====================================================================================================================
    private static readonly (string Code, string Title, int Units, string Department)[] MySubjects =
    {
    ("EM 1A1", "Engineering Calculus 1", 4, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("NS 1A1", "Chemistry for Engineers (Lec)", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("NS 1A2", "Chemistry for Engineers (Lab)", 1, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("CPE 1A1", "Computer Engineering as a Discipline", 1, "Department of Computer Engineering"),
    ("CPE 1A2", "Fundamentals in Programming", 3, "Department of Computer Engineering"),
    ("GE RPH", "Readings in Philippine History", 3, "Department of Social Sciences and Philosophy"),
    ("GE MMW", "Mathematics in the Modern World", 3, "Department of Mathematics and Sciences"),
    ("GE TCW", "The Contemporary World", 3, "Department of Communication, Languages, and Literature"),
    ("PE 1", "Physical Education 1", 2, "Department of Physical Education"),
    ("CWTS 11*", "Civic Welfare Training Services 11", 3, ""),
    ("ReEd 1", "Initium Fidei: An Introduction to Doing Catholic Theology", 3, ""),
    ("Guidance 1", "Adjustment to College Life Phase 1", 1, ""),
    // 1st Year 2nd Semester
    ("EM 1B1", "Engineering Calculus 2", 4, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("NS 1B1", "Physics for Engineers (Lec)", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("NS 1B2", "Physics for Engineers (Lab)", 1, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("CPE 1B1", "Programming Logic and Design", 3, "Department of Computer Engineering"),
    ("GE STS", "Science, Technology and Society", 3, "Department of Mathematics and Sciences"),
    ("Lit 1", "The Literatures of the Philippines", 3, "Department of Communication, Languages, and Literature"),
    ("EP 1", "English Proficiency Level 1 (lec/lab)", 3, "Department of Communication, Languages, and Literature"),
    ("PE 2", "Physical Education 2", 2, "Department of Physical Education"),
    ("CWTS 12*", "Civic Welfare Training Services 12", 3, ""),
    ("ReEd 2", "Written That You May Believe: An Introduction to Biblical Exegesis", 3, ""),
    ("Guidance 2", "Adjustment to College Life Phase 2", 1, ""),
    // 2nd Year 1st Semester
    ("ES 2A", "Computer-Aided Drafting", 2, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("EM 2A1", "Differential Equations", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("EDA 1", "Engineering Data Analysis", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("CPE 2A1", "Object-Oriented Programming", 3, "Department of Computer Engineering"),
    ("CPE 2A3", "Discrete Mathematics", 3, "Department of Computer Engineering"),
    ("ES 6", "Engineering Economics", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("AC 2A1", "Fundamentals of Electrical Circuits", 3, "Department of Electrical Engineering"),
    ("AC 2A2", "Fundamentals of Electrical Circuits", 1, "Department of Electrical Engineering"),
    ("GE PC", "Purposive Communication", 3, "Department of Communication, Languages, and Literature"),
    ("PE 3", "Physical Education 3", 2, "Department of Physical Education"),
    ("ReEd 3", "Our Restless Hearts: An Introduction to Doing Catholic Morality", 3, ""),
    // 2nd Year 2nd Semester
    ("CPE 2B1", "Advanced Engineering Mathematics for Computer Engineering", 3, "Department of Computer Engineering"),
    ("CPE 2B3", "Data Structures and Algorithms", 3, "Department of Computer Engineering"),
    ("GE AA", "Art Appreciation", 3, "Department of Communication, Languages, and Literature"),
    ("AC 2B1", "Fundamentals of Electronic Circuits (Lec)", 3, "Department of Electronics Engineering"),
    ("AC 2B2", "Fundamentals of Electronic Circuits (Lab)", 1, "Department of Electronics Engineering"),
    ("ES 8", "Engineering Mechanics", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("Rizal", "Life and Works of Dr. Jose Rizal", 3, "Department of Social Sciences and Philosophy"),
    ("PE 4", "Physical Education 4", 2, "Department of Physical Education"),
    ("ReEd 4", "A Call to Action: An Introduction to Catholic Social Thought", 3, ""),
    ("EfCom", "Effective Communication & Human Relations", 3, "Department of Communication, Languages, and Literature"),
    // 2nd Year Summer
    ("CPE 2S1", "Software Design", 4, "Department of Computer Engineering"),
    ("CPE 2S2", "Data and Digital Communications", 3, "Department of Computer Engineering"),
    ("CPE 2S4", "Computer Engineering Drafting and Design", 2, "Department of Computer Engineering"),
    // 3rd Year 1st Semester
    ("CPE 3A1", "Numerical Methods", 3, "Department of Computer Engineering"),
    ("CPE 3A3", "Logic Circuits and Design (Lec)", 3, "Department of Computer Engineering"),
    ("CPE 3A4", "Logic Circuits and Designs (Lab)", 1, "Department of Computer Engineering"),
    ("ES 7", "Engineering Management", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("CPE 3A5", "Computer Networks and Security (Lec)", 3, "Department of Computer Engineering"),
    ("CPE 3A6", "Computer Networks and Security (Lab)", 1, "Department of Computer Engineering"),
    ("AC 3A3", "Fundamentals of Mixed Signals and Sensors", 3, "Department of Electronics Engineering"),
    ("EDA 2", "Advanced Engineering Data Analysis", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("CPE COG1", "Cognate/Elective Course 1", 3, "Department of Computer Engineering"),
    ("GE ET", "Ethics", 3, "Department of Social Sciences and Philosophy"),
    // 3rd Year 2nd Semester
    ("CPE 3B1", "Basic Occupational Health and Safety", 3, "Department of Computer Engineering"),
    ("CPE 3B2", "Project Management for Computer Engineers", 3, "Department of Computer Engineering"),
    ("CPE 3B3", "Microprocessors (Lec)", 3, "Department of Computer Engineering"),
    ("CPE 3B4", "Micrprocessors (Lab)", 1, "Department of Computer Engineering"),
    ("CPE 3B5", "Methods of Research", 3, "Department of Computer Engineering"),
    ("CPE 3B7", "CpE Laws and Professional Practice", 3, "Department of Computer Engineering"),
    ("CPE COG2", "Cognate/Elective Course 2", 3, "Department of Computer Engineering"),
    ("GE UTS", "Understanding the Self", 3, "Department of Psychology and Library Information Science"),
    ("AC 3B1", "Feedback and Control Systems", 3, "Department of Electronics Engineering"),
    // 3rd Year Summer
    ("CPE 3S1", "Introduction to HDL", 2, "Department of Computer Engineering"),
    ("CPE 3S3", "Embedded Systems (Lec)", 3, "Department of Computer Engineering"),
    ("CPE 3S4", "Embedded Systems (Lab)", 1, "Department of Computer Engineering"),
    ("CPE 3S5", "Database Management Systems", 3, "Department of Computer Engineering"),
    // 4th Year 1st Semester
    ("CPE 4A1", "Computer Architecture and Organization (Lec)", 3, "Department of Computer Engineering"),
    ("CPE 4A2", "Computer Architecture and Organization (Lab)", 1, "Department of Computer Engineering"),
    ("CPE 4A3", "Emerging Technologies in CpE", 3, "Department of Computer Engineering"),
    ("CPE 4A5", "CpE Practice and Design 1", 2, "Department of Computer Engineering"),
    ("CPE 4A7", "Digital Signal Processing (Lec)", 3, "Department of Computer Engineering"),
    ("CPE 4A8", "Digital Signal Processing (Lab)", 1, "Department of Computer Engineering"),
    ("CPE 4A9", "Operating Systems", 3, "Department of Computer Engineering"),
    ("ES 12", "Technopreneurship 101", 3, "Department of Engineering, Mathematics, and Enhancement Program"),
    ("GE EPM", "Eastern Philosophy", 3, "Department of Computer Engineering"),
    // 4th Year 2nd Semester
    ("CPE 4B1", "CpE Practice and Design", 3, "Department of Computer Engineering"),
    ("CPE COG3", "Cognate/Elective Course 3", 3, "Department of Computer Engineering"),
    ("CPE 4B3", "CpE on the Job Training (240 Hours Minimum)", 3, "Department of Computer Engineering"),
    ("CPE 4B4", "Seminars and Field Trips", 1, "Department of Computer Engineering"),


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
        // Alexis Dacanay (30001)
        ("30001", "CPE 1A2", DayOfWeek.Monday, "08:00", "09:00", "SA", "B-1", "B-1-01"),
        ("30001", "CPE 1A2", DayOfWeek.Wednesday, "08:00", "09:00", "SA", "B-1", "B-1-01"),
        ("30001", "CPE 1A2", DayOfWeek.Friday, "08:00", "09:00", "SA", "B-1", "B-1-01"),
        ("30001", "CPE 2A1", DayOfWeek.Monday, "10:00", "11:00", "SA", "B-1", "B-1-02"),
        ("30001", "CPE 2A1", DayOfWeek.Wednesday, "10:00", "11:00", "SA", "B-1", "B-1-02"),
        ("30001", "CPE 2A1", DayOfWeek.Friday, "10:00", "11:00", "SA", "B-1", "B-1-02"),
        ("30001", "CPE 2A3", DayOfWeek.Monday, "13:00", "14:00", "SA", "2", "2-01"),
        ("30001", "CPE 2A3", DayOfWeek.Wednesday, "13:00", "14:00", "SA", "2", "2-01"),
        ("30001", "CPE 2A3", DayOfWeek.Friday, "13:00", "14:00", "SA", "2", "2-01"),
        ("30001", "CPE 1A1", DayOfWeek.Tuesday, "13:00", "14:00", "SA", "B-1", "B-1-01"),
        // Jeren Yacanad (30002)
        ("30002", "CPE 2B3", DayOfWeek.Monday, "09:00", "10:00", "SA", "B-2", "B-2-01"),
        ("30002", "CPE 2B3", DayOfWeek.Wednesday, "09:00", "10:00", "SA", "B-2", "B-2-01"),
        ("30002", "CPE 2B3", DayOfWeek.Friday, "09:00", "10:00", "SA", "B-2", "B-2-01"),
        ("30002", "CPE 3A1", DayOfWeek.Monday, "14:00", "15:00", "SA", "B-2", "B-2-02"),
        ("30002", "CPE 3A1", DayOfWeek.Wednesday, "14:00", "15:00", "SA", "B-2", "B-2-02"),
        ("30002", "CPE 3A1", DayOfWeek.Friday, "14:00", "15:00", "SA", "B-2", "B-2-02"),
        ("30002", "CPE 2S4", DayOfWeek.Tuesday, "10:30", "12:00", "SA", "2", "2-02"),
        ("30002", "CPE 2S4", DayOfWeek.Thursday, "10:30", "12:00", "SA", "2", "2-02"),
        ("30002", "CPE 3A4", DayOfWeek.Tuesday, "13:00", "16:00", "SA", "B-2", "B-2-03"),
        // Robel Branianola (30003)
        ("30003", "CPE 3A5", DayOfWeek.Monday, "08:00", "09:00", "SA", "2", "2-04"),
        ("30003", "CPE 3A5", DayOfWeek.Wednesday, "08:00", "09:00", "SA", "2", "2-04"),
        ("30003", "CPE 3A5", DayOfWeek.Friday, "08:00", "09:00", "SA", "2", "2-04"),
        ("30003", "CPE 4A1", DayOfWeek.Monday, "11:00", "12:00", "SA", "B-1", "B-1-03"),
        ("30003", "CPE 4A1", DayOfWeek.Wednesday, "11:00", "12:00", "SA", "B-1", "B-1-03"),
        ("30003", "CPE 4A1", DayOfWeek.Friday, "11:00", "12:00", "SA", "B-1", "B-1-03"),
        ("30003", "CPE 4A9", DayOfWeek.Monday, "15:00", "16:00", "SA", "2", "2-05"),
        ("30003", "CPE 4A9", DayOfWeek.Wednesday, "15:00", "16:00", "SA", "2", "2-05"),
        ("30003", "CPE 4A9", DayOfWeek.Friday, "15:00", "16:00", "SA", "2", "2-05"),
        ("30003", "CPE 4A5", DayOfWeek.Tuesday, "08:00", "09:30", "SA", "B-1", "B-1-04"),
        ("30003", "CPE 4A5", DayOfWeek.Thursday, "08:00", "09:30", "SA", "B-1", "B-1-04"),
        ("30003", "CPE 4A2", DayOfWeek.Thursday, "13:00", "16:00", "SA", "B-1", "B-1-05"),
        // Sean Pareja (Chairperson) (20001)
        ("20001", "CPE 3B3", DayOfWeek.Monday, "10:00", "11:00", "SA", "B-2", "B-2-04"),
        ("20001", "CPE 3B3", DayOfWeek.Wednesday, "10:00", "11:00", "SA", "B-2", "B-2-04"),
        ("20001", "CPE 3B3", DayOfWeek.Friday, "10:00", "11:00", "SA", "B-2", "B-2-04"),
        ("20001", "CPE 3B5", DayOfWeek.Monday, "13:00", "14:00", "SA", "2", "2-06"),
        ("20001", "CPE 3B5", DayOfWeek.Wednesday, "13:00", "14:00", "SA", "2", "2-06"),
        ("20001", "CPE 3B5", DayOfWeek.Friday, "13:00", "14:00", "SA", "2", "2-06"),
        ("20001", "CPE 3B7", DayOfWeek.Monday, "16:00", "17:00", "SA", "2", "2-07"),
        ("20001", "CPE 3B7", DayOfWeek.Wednesday, "16:00", "17:00", "SA", "2", "2-07"),
        ("20001", "CPE 3B7", DayOfWeek.Friday, "16:00", "17:00", "SA", "2", "2-07"),
        ("20001", "CPE 3B4", DayOfWeek.Tuesday, "08:00", "11:00", "SA", "B-2", "B-2-05"),
        // Dennis Trillo (Dean) (10001)
        ("10001", "ES 8", DayOfWeek.Monday, "09:00", "10:00", "SA", "2", "2-08"),
        ("10001", "ES 8", DayOfWeek.Wednesday, "09:00", "10:00", "SA", "2", "2-08"),
        ("10001", "ES 8", DayOfWeek.Friday, "09:00", "10:00", "SA", "2", "2-08"),
        ("10001", "AC 2A1", DayOfWeek.Monday, "11:00", "12:00", "SA", "B-2", "B-2-06"),
        ("10001", "AC 2A1", DayOfWeek.Wednesday, "11:00", "12:00", "SA", "B-2", "B-2-06"),
        ("10001", "AC 2A1", DayOfWeek.Friday, "11:00", "12:00", "SA", "B-2", "B-2-06"),
        ("10001", "EM 1A1", DayOfWeek.Tuesday, "13:00", "15:00", "SA", "B-1", "B-1-06"),
        ("10001", "EM 1A1", DayOfWeek.Thursday, "13:00", "15:00", "SA", "B-1", "B-1-06"),
        ("10001", "ES 2A", DayOfWeek.Tuesday, "15:30", "17:00", "SA", "2", "2-09"),
        ("10001", "ES 2A", DayOfWeek.Thursday, "15:30", "17:00", "SA", "2", "2-09"),
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
