using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record ScholarInfo(string UserId, string Name, string Label, string AssignedSchool, string OwnSchool)
{
    public string Assignment
    {
        get
        {
            var text = AssignedSchool.Length == 0 ? "Not assigned" : $"Monitors {AssignedSchool}";
            return OwnSchool.Length == 0 ? text : $"{text} (studies in {OwnSchool})";
        }
    }
}

public sealed record OperationResult(bool Success, string Message);

/// <summary>
/// IMS Personnel assign READS Scholars to a school (all of its departments). A Scholar is never assigned to the school they study in,
/// so they do not check the faculty of their own school. Only IMS Personnel may use it.
/// </summary>
public sealed class ScholarAssignmentService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;

    public ScholarAssignmentService(JsonStorageService storage, SessionService session)
    {
        _storage = storage;
        _session = session;
    }

    private bool IsIms => _session.Role == UserRole.ImsPersonnel;

    public async Task<List<ScholarInfo>> GetScholarsAsync()
    {
        if (!IsIms) return new();
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return users
            .Where(u => u.Role == UserRole.ReadsScholar && u.IsUsable)
            .OrderBy(u => u.FullName)
            .Select(u => new ScholarInfo(u.Id, u.FullName, $"{u.FullName} ({u.SisId})", u.AssignedSchool, u.OwnSchool))
            .ToList();
    }

    public Task<OperationResult> AssignAsync(string userId, string school)
    {
        if (!AcademicCatalog.IsValidSchool(school))
            return Task.FromResult(new OperationResult(false, "Choose a school."));
        return UpdateAsync(userId, school);
    }

    public Task<OperationResult> ClearAsync(string userId) => UpdateAsync(userId, "");

    private async Task<OperationResult> UpdateAsync(string userId, string school)
    {
        if (!IsIms) return new OperationResult(false, "Only IMS Personnel can assign READS Scholars.");

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var i = users.FindIndex(u => u.Id == userId && u.Role == UserRole.ReadsScholar);
        if (i < 0) return new OperationResult(false, "That READS Scholar was not found.");

        var name = users[i].FullName;
        if (school.Length > 0 && string.Equals(users[i].OwnSchool, school, StringComparison.OrdinalIgnoreCase))
            return new OperationResult(false, $"{name} studies in the {school}. Assign a different school, so they do not check the faculty of their own school.");

        users[i] = users[i] with { AssignedSchool = school, AssignedDepartment = "" };
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        return new OperationResult(true, school.Length == 0
            ? $"{name} is no longer assigned to a school."
            : $"{name} assigned to {school}.");
    }
}
