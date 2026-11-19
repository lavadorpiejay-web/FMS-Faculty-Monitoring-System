using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record DirectoryResult(
    bool IsAssigned, string School, string Department,
    IReadOnlyList<DirectoryEntry> Deans,
    IReadOnlyList<DirectoryEntry> Chairpersons,
    IReadOnlyList<DirectoryEntry> Faculty)
{
    public static DirectoryResult NotAssigned { get; } = new(false, "", "",
        Array.Empty<DirectoryEntry>(), Array.Empty<DirectoryEntry>(), Array.Empty<DirectoryEntry>());
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
        if (me is null || !AcademicCatalog.IsValidDepartment(me.AssignedSchool, me.AssignedDepartment))
            return DirectoryResult.NotAssigned;

        var school = me.AssignedSchool;
        var dept = me.AssignedDepartment;
        var faculty = Visible(await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile), users);

        var deans = users
            .Where(u => u.IsUsable && u.Role == UserRole.Dean && u.School == school)
            .OrderBy(u => u.FullName)
            .Select(u => new DirectoryEntry(u.FullName, $"Dean, {u.School}"))
            .ToList();

        var chairs = users
            .Where(u => u.IsUsable && u.Role == UserRole.Chairperson && u.School == school && u.Department == dept)
            .OrderBy(u => u.FullName)
            .Select(u => new DirectoryEntry(u.FullName, $"Chairperson, {u.Department}"))
            .ToList();

        var members = faculty
            .Where(f => f.School == school && f.Department == dept)
            .OrderBy(f => f.FullName)
            .Select(f => new DirectoryEntry(f.FullName, $"Faculty, {f.Department}"))
            .ToList();

        return new DirectoryResult(true, school, dept, deans, chairs, members);
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
            .Select(f => new DirectoryEntry(f.FullName, $"Faculty, {f.Department}"))
            .ToList();

        return new DepartmentFacultyResult(true, me.School, me.Department, members);
    }
}
