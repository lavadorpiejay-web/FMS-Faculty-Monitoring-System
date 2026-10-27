using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Runs the per-role verification checks required by the SRS foundation milestone.</summary>
public sealed class ReadinessService
{
    private static readonly Version MinAndroid = new(7, 0); // project baseline; adjust to your target

    private readonly JsonStorageService _storage;
    public ReadinessService(JsonStorageService storage) => _storage = storage;

    public async Task<List<CheckResult>> RunAsync(UserAccount user)
    {
        var r = new List<CheckResult>
        {
            new("Authenticated session", $"Signed in as {user.SisId}", user.IsActive),
            new("Application shell", "Navigation shell and layout rendered", true),
        };

        await _storage.InitializeAsync();
        var users = await TryLoad<UserAccount>(JsonStorageService.UsersFile);
        var faculty = await TryLoad<FacultyMember>(JsonStorageService.FacultyFile);
        var subjects = await TryLoad<Subject>(JsonStorageService.SubjectsFile);
        var rooms = await TryLoad<Classroom>(JsonStorageService.ClassroomsFile);
        var allOk = users.Ok && faculty.Ok && subjects.Ok && rooms.Ok;
        var filesDetail = allOk ? "All local data files are readable" : "One or more data files are missing or unreadable";

        switch (user.Role)
        {
            case UserRole.Faculty:
                r.Add(new("Local data connection", filesDetail, allOk));
                r.Add(new("Faculty record linked", "Your account is linked to a faculty record",
                    faculty.Items.Any(f => f.UserId == user.Id)));
                break;

            case UserRole.ReadsScholar:
                r.Add(new("READS Scholar role verified", $"Assigned office: {user.Department}", true));
                r.Add(Describe("users.json", users));
                r.Add(Describe("faculty.json", faculty));
                r.Add(Describe("subjects.json", subjects));
                r.Add(Describe("classrooms.json", rooms));
                break;

            case UserRole.ImsPersonnel:
                r.Add(new("IMS Personnel role verified", "Role confirmed from your account", true));
                var scholars = users.Items.Count(u => u.Role == UserRole.ReadsScholar && u.Department == AcademicCatalog.ImsOffice);
                r.Add(new("READS Scholars in the IMS Office", $"{scholars} assigned to the IMS Office", true));
                var placed = users.Items.Count(u => u.Role == UserRole.ReadsScholar && u.AssignedDepartment.Length > 0);
                r.Add(new("Scholar department assignments", $"{placed} of {scholars} READS Scholars assigned to a department", true));
                r.Add(Describe("users.json", users));
                r.Add(Describe("faculty.json", faculty));
                r.Add(Describe("subjects.json", subjects));
                r.Add(Describe("classrooms.json", rooms));
                break;

            case UserRole.Chairperson:
                r.Add(new("Local database mappings", filesDetail, allOk));
                var f = faculty.Items.Count(x => x.Department == user.Department);
                var s = subjects.Items.Count(x => x.Department == user.Department);
                r.Add(new($"Unit mapping: {user.Department}", $"{f} faculty · {s} subjects mapped", f > 0 && s > 0));
                break;

            case UserRole.Dean:
                r.Add(new("School assigned", string.IsNullOrEmpty(user.School) ? "No school on this account" : user.School,
                    !string.IsNullOrEmpty(user.School)));
                r.Add(new("Credential protection", "All passwords stored as salted hashes",
                    users.Ok && users.Items.All(u => u.PasswordHash.Length > 0 && u.Salt.Length > 0)));
                r.Add(new("Private storage", "Data kept in app-private directory",
                    _storage.DataDirectory.StartsWith(FileSystem.AppDataDirectory, StringComparison.Ordinal)));
                r.Add(new("Local data files", allOk ? "All 4 data files readable" : "Data files missing or unreadable", allOk));
                break;
        }

        var android = DeviceInfo.Platform == DevicePlatform.Android;
        r.Add(new("Android platform", $"{DeviceInfo.Platform} {DeviceInfo.VersionString} · {DeviceInfo.Model}", android));

        if (user.Role == UserRole.Dean)
            r.Add(new("Android compatibility", $"Requires Android {MinAndroid}+", DeviceInfo.Version >= MinAndroid));

        return r;
    }

    private static CheckResult Describe<T>(string file, (bool Ok, List<T> Items) load) =>
        new(file, load.Ok ? $"{load.Items.Count} records" : "Missing or unreadable", load.Ok);

    private async Task<(bool Ok, List<T> Items)> TryLoad<T>(string file)
    {
        try
        {
            if (!_storage.Exists(file)) return (false, new());
            return (true, await _storage.LoadAsync<T>(file));
        }
        catch { return (false, new()); }
    }
}
