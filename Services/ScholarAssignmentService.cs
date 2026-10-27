using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed record ScholarInfo(string UserId, string Name, string Label, string AssignedSchool, string AssignedDepartment)
{
    public string Assignment =>
        AssignedDepartment.Length == 0 ? "Not assigned" : $"{AssignedDepartment}, {AssignedSchool}";
}

public sealed record OperationResult(bool Success, string Message);

/// <summary>IMS Personnel assigns READS Scholars to a school and department. Only IMS Personnel may use it.</summary>
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
            .Where(u => u.Role == UserRole.ReadsScholar && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new ScholarInfo(u.Id, u.FullName, $"{u.FullName} ({u.SisId})", u.AssignedSchool, u.AssignedDepartment))
            .ToList();
    }

    public Task<OperationResult> AssignAsync(string userId, string school, string department)
    {
        if (!AcademicCatalog.IsValidDepartment(school, department))
            return Task.FromResult(new OperationResult(false, "Choose a school and one of its departments."));
        return UpdateAsync(userId, school, department);
    }

    public Task<OperationResult> ClearAsync(string userId) => UpdateAsync(userId, "", "");

    private async Task<OperationResult> UpdateAsync(string userId, string school, string department)
    {
        if (!IsIms) return new OperationResult(false, "Only IMS Personnel can assign READS Scholars.");

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var i = users.FindIndex(u => u.Id == userId && u.Role == UserRole.ReadsScholar);
        if (i < 0) return new OperationResult(false, "That READS Scholar was not found.");

        var name = users[i].FullName;
        users[i] = users[i] with { AssignedSchool = school, AssignedDepartment = department };
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        return new OperationResult(true, department.Length == 0
            ? $"{name} is no longer assigned to a department."
            : $"{name} assigned to {department}, {school}.");
    }
}
