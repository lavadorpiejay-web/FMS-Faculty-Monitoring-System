using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record DirectoryResult(
    bool IsAssigned, string School, string Department,
    IReadOnlyList<DirectoryEntry> Deans,
    IReadOnlyList<DirectoryEntry> Chairpersons,
    IReadOnlyList<DirectoryEntry> Faculty,
    IReadOnlyList<DepartmentGroup> Departments)
{
    public static DirectoryResult NotAssigned { get; } = new(false, "", "",
        Array.Empty<DirectoryEntry>(), Array.Empty<DirectoryEntry>(), Array.Empty<DirectoryEntry>(),
        Array.Empty<DepartmentGroup>());
}

public sealed record SchoolDirectoryResult(bool IsValid, string School, IReadOnlyList<DepartmentGroup> Departments);

public sealed record DepartmentFacultyResult(bool IsValid, string School, string Department, IReadOnlyList<DirectoryEntry> Faculty);

/// <summary>
/// Read-only directories. READS Scholar: the Dean, Chairperson and Faculty of the assigned department.
/// Dean: every department of their own school. Chairperson: the Faculty of their own department.
/// The school and department always come from the stored account, never from the screen.
/// </summary>
public sealed class DirectoryService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;

    public DirectoryService(JsonStorageService storage, SessionService session)
    {
        _storage = storage;
        _session = session;
    }

    // Faculty whose account is still unverified or waiting for approval are not listed yet.
    private static List<FacultyMember> Visible(List<FacultyMember> faculty, List<UserAccount> users)
    {
        var usable = users.Where(u => u.IsUsable).Select(u => u.Id).ToHashSet();
        return faculty.Where(f => f.UserId is null || usable.Contains(f.UserId)).ToList();
    }

    public async Task<DirectoryResult> LoadForCurrentScholarAsync()
    {
        var current = _session.CurrentUser;
        if (current is null || current.Role != UserRole.ReadsScholar) return DirectoryResult.NotAssigned;

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);

        // Re-read the account so an assignment made by IMS Personnel shows up without signing out.
        var me = users.FirstOrDefault(u => u.Id == current.Id && u.IsActive && u.Role == UserRole.ReadsScholar);
        if (me is null || !AcademicCatalog.IsValidUnit(me.AssignedSchool))
            return DirectoryResult.NotAssigned;

        // A READS Scholar is assigned to a whole school: every department's Chairperson and Faculty.
        var school = me.AssignedSchool;
        const string dept = "All departments";
        var faculty = Visible(await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile), users);

        var deans = users
            .Where(u => u.IsUsable && u.Role == UserRole.Dean && u.School == school)
            .OrderBy(u => u.FullName)
            .Select(u => UserEntry(u, "Dean", $"Dean, {u.School}"))
            .ToList();

        var chairs = users
            .Where(u => u.IsUsable && u.Role == UserRole.Chairperson && u.School == school)
            .OrderBy(u => u.Department).ThenBy(u => u.FullName)
            .Select(u => UserEntry(u, "Chairperson", $"Chairperson, {u.Department}"))
            .ToList();

        var members = faculty
            .Where(f => f.School == school)
            .OrderBy(f => f.Department).ThenBy(f => f.FullName)
            .Select(f => FacultyEntry(f))
            .ToList();

        // The same people grouped by department, for the READS Directory (department > chairperson > faculty).
        var departments = AcademicCatalog.DepartmentsOf(school).Select(d => new DepartmentGroup(
            d,
            users.Where(u => u.IsUsable && u.Role == UserRole.Chairperson && u.School == school && u.Department == d)
                 .OrderBy(u => u.FullName)
                 .Select(u => UserEntry(u, "Chairperson", $"Chairperson, {d}")).ToList(),
            faculty.Where(f => f.School == school && f.Department == d)
                   .OrderBy(f => f.FullName)
                   .Select(f => FacultyEntry(f)).ToList()))
            .ToList();

        return new DirectoryResult(true, school, dept, deans, chairs, members, departments);
    }

    private static DirectoryEntry UserEntry(UserAccount u, string role, string detail) =>
        new(u.FullName, detail, u.Id, role, u.SisId, u.Email, u.School, u.Department);

    private static DirectoryEntry FacultyEntry(FacultyMember f) =>
        new(f.FullName, $"Faculty, {f.Department}", f.UserId ?? "", "Faculty", f.EmployeeNo, f.Email, f.School, f.Department);

    /// <summary>
    /// READS Scholar: the full page of one Dean, Chairperson or Faculty member of the assigned school. Faculty: the subjects they teach
    /// with every day, time and room. Chairperson: the subjects of the department and its Faculty. Dean: the Chairpersons of the school.
    /// The person must belong to the Scholar's assigned school (re-read from storage), never just to what the screen sent.
    /// </summary>
    public async Task<PersonDetail> LoadPersonAsync(DirectoryEntry entry)
    {
        var invalid = new PersonDetail(false, entry.Name, "", "", "", "", "", false, "", Array.Empty<ListRow>(), "", Array.Empty<ListRow>());
        var current = _session.CurrentUser;
        if (current is null || current.Role != UserRole.ReadsScholar) return invalid;

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var me = users.FirstOrDefault(u => u.Id == current.Id && u.IsActive && u.Role == UserRole.ReadsScholar);
        if (me is null || !AcademicCatalog.IsValidUnit(me.AssignedSchool) || entry.School != me.AssignedSchool) return invalid;

        var faculty = Visible(await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile), users);
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var rooms = (await _storage.LoadAsync<Classroom>(JsonStorageService.ClassroomsFile)).ToDictionary(r => r.Id);
        var subjects = await _storage.LoadAsync<Subject>(JsonStorageService.SubjectsFile);
        static int DayOrder(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

        var subjectRows = new List<ListRow>();

        // Anyone who handles subjects (Faculty, and Chairpersons or Deans who teach) shows their classes the same way.
        var subjectsHeading = "Subjects and schedule";
        if (entry.UserId.Length > 0)
            foreach (var g in schedules.Where(s => s.FacultyUserId == entry.UserId).GroupBy(s => s.SubjectCode).OrderBy(g => g.Key))
            {
                var lines = g.OrderBy(s => DayOrder(s.Day)).ThenBy(s => s.Start).Select(s =>
                {
                    rooms.TryGetValue(s.ClassroomId, out var room);
                    return $"{s.Day} {TimeFormat.Range(s.Start, s.End)} | {ScheduleRules.RoomLabel(room)}";
                });
                subjectRows.Add(new ListRow(g.Key, $"{g.Key} - {g.First().SubjectTitle}", string.Join("\n", lines)));
            }

        var related = new List<ListRow>();
        var relatedHeading = "";
        if (entry.Role == "Chairperson")
        {
            relatedHeading = "Faculty of the department";
            related.AddRange(faculty.Where(f => f.School == entry.School && f.Department == entry.Department).OrderBy(f => f.FullName)
                .Select(f => new ListRow(f.UserId ?? f.Id, f.FullName, "Faculty")));
        }
        else if (entry.Role == "Dean")
        {
            relatedHeading = "Chairpersons of the school";
            related.AddRange(users.Where(u => u.IsUsable && u.Role == UserRole.Chairperson && u.School == entry.School)
                .OrderBy(u => u.Department).ThenBy(u => u.FullName)
                .Select(u => new ListRow(u.Id, u.FullName, u.Department)));
        }

        var roleText = entry.Role.Length > 0 ? entry.Role : "Faculty";
        return new PersonDetail(true, entry.Name, roleText, entry.SisId, entry.Email, entry.School, entry.Department,
            entry.UserId.Length > 0, subjectsHeading, subjectRows, relatedHeading, related);   // IsFaculty = can be checked: has an account that holds classes
    }

    /// <summary>Dean: every department of the Dean's own school, each with its Chairperson(s) and Faculty.</summary>
    public async Task<SchoolDirectoryResult> LoadForCurrentDeanAsync()
    {
        var invalid = new SchoolDirectoryResult(false, "", Array.Empty<DepartmentGroup>());
        var current = _session.CurrentUser;
        if (current is null || current.Role != UserRole.Dean) return invalid;

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var me = users.FirstOrDefault(u => u.Id == current.Id && u.IsActive && u.Role == UserRole.Dean);
        if (me is null || !AcademicCatalog.IsValidSchool(me.School)) return invalid;

        var faculty = Visible(await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile), users);

        var groups = AcademicCatalog.DepartmentsOf(me.School).Select(dept => new DepartmentGroup(
            dept,
            users.Where(u => u.IsUsable && u.Role == UserRole.Chairperson && u.School == me.School && u.Department == dept)
                 .OrderBy(u => u.FullName)
                 .Select(u => new DirectoryEntry(u.FullName, $"Chairperson, {dept}")).ToList(),
            faculty.Where(f => f.School == me.School && f.Department == dept)
                   .OrderBy(f => f.FullName)
                   .Select(f => new DirectoryEntry(f.FullName, $"Faculty, {dept}")).ToList()))
            .ToList();

        return new SchoolDirectoryResult(true, me.School, groups);
    }

    /// <summary>Chairperson: the Faculty of the Chairperson's own department.</summary>
    public async Task<DepartmentFacultyResult> LoadForCurrentChairpersonAsync()
    {
        var invalid = new DepartmentFacultyResult(false, "", "", Array.Empty<DirectoryEntry>());
        var current = _session.CurrentUser;
        if (current is null || current.Role != UserRole.Chairperson) return invalid;

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var me = users.FirstOrDefault(u => u.Id == current.Id && u.IsActive && u.Role == UserRole.Chairperson);
        if (me is null || !AcademicCatalog.IsValidDepartment(me.School, me.Department)) return invalid;

        var faculty = Visible(await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile), users);
        var members = faculty
            .Where(f => f.School == me.School && f.Department == me.Department)
            .OrderBy(f => f.FullName)
            .Select(f => FacultyEntry(f))
            .ToList();

        return new DepartmentFacultyResult(true, me.School, me.Department, members);
    }
}
