using System.Text.Json;
using System.Text.Json.Serialization;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>Local JSON storage for users, faculty, subjects and classrooms (app-private directory).</summary>
public sealed class JsonStorageService
{
    public const string UsersFile = "users.json";
    public const string FacultyFile = "faculty.json";
    public const string SubjectsFile = "subjects.json";
    public const string ClassroomsFile = "classrooms.json";
    public const string VerificationsFile = "verifications.json";
    public const string SchedulesFile = "schedules.json";
    public const string DutiesFile = "duties.json";
    public const string VacanciesFile = "vacancies.json";
    public const string CheckAssignmentsFile = "checkassignments.json";
    public const string RequestsFile = "requests.json";
    public const string AttendanceFile = "attendance.json";
    public const string IrregularitiesFile = "irregularities.json";
    public const string AuditFile = "audit.json";
    public const string NotificationsFile = "notifications.json";
    public const string ResignationsFile = "resignations.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public string DataDirectory => Path.Combine(FileSystem.AppDataDirectory, "data");
    public string PathOf(string file) => Path.Combine(DataDirectory, file);
    public bool Exists(string file) => File.Exists(PathOf(file));

    /// <summary>Creates the data folder and seeds any missing file. Safe to call repeatedly.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _gate.WaitAsync();
        try
        {
            if (_initialized) return;
            Directory.CreateDirectory(DataDirectory);

            if (!Exists(UsersFile))      await WriteAsync(UsersFile, await Task.Run(SeedData.Users));
            if (!Exists(FacultyFile))    await WriteAsync(FacultyFile, SeedData.Faculty());
            if (!Exists(SubjectsFile))   await WriteAsync(SubjectsFile, SeedData.Subjects());
            if (!Exists(ClassroomsFile)) await WriteAsync(ClassroomsFile, SeedData.Classrooms());
            if (!Exists(VerificationsFile)) await WriteAsync(VerificationsFile, new List<VerificationCode>());
            if (!Exists(SchedulesFile))  await WriteAsync(SchedulesFile, SeedData.Schedules());
            if (!Exists(DutiesFile))     await WriteAsync(DutiesFile, SeedData.Duties());
            if (!Exists(VacanciesFile))  await WriteAsync(VacanciesFile, SeedData.Vacancies());
            if (!Exists(CheckAssignmentsFile)) await WriteAsync(CheckAssignmentsFile, SeedData.CheckAssignments());
            if (!Exists(RequestsFile))   await WriteAsync(RequestsFile, new List<ChangeRequest>());
            if (!Exists(AttendanceFile)) await WriteAsync(AttendanceFile, new List<AttendanceLog>());
            if (!Exists(IrregularitiesFile)) await WriteAsync(IrregularitiesFile, new List<IrregularityReport>());
            if (!Exists(AuditFile))      await WriteAsync(AuditFile, new List<AuditEntry>());
            if (!Exists(NotificationsFile)) await WriteAsync(NotificationsFile, new List<AppNotification>());
            if (!Exists(ResignationsFile)) await WriteAsync(ResignationsFile, new List<ResignationRequest>());

            _initialized = true;
        }
        finally { _gate.Release(); }
    }

    public async Task<List<T>> LoadAsync<T>(string file)
    {
        await using var stream = File.OpenRead(PathOf(file));
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, Options) ?? new List<T>();
    }

    public async Task SaveAsync<T>(string file, List<T> items)
    {
        await _gate.WaitAsync();
        try { await WriteAsync(file, items); }
        finally { _gate.Release(); }
    }

    // Write to a temp file then replace, so a crash never leaves a half-written JSON file.
    private async Task WriteAsync<T>(string file, List<T> items)
    {
        var path = PathOf(file);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(items, Options));
        File.Move(temp, path, overwrite: true);
    }
}
