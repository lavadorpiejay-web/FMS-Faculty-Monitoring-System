using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string sisId, string password);
    Task<AuthResult> RegisterAsync(RegistrationRequest request);
}


public sealed class LocalAuthService : IAuthService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(1);
    private const string Invalid = "Invalid SIS ID or password.";

    private readonly JsonStorageService _storage;
    private readonly Dictionary<string, (int Fails, DateTime? LockedUntil)> _attempts = new();

    public LocalAuthService(JsonStorageService storage) => _storage = storage;

    public async Task<AuthResult> SignInAsync(string sisId, string password)
    {
        var key = sisId.Trim().ToLowerInvariant();

        if (_attempts.TryGetValue(key, out var state) && state.LockedUntil is { } until && until > DateTime.UtcNow)
        {
            var secs = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds);
            return new AuthResult(false, $"Too many attempts. Try again in {secs}s.", null);
        }

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var user = users.FirstOrDefault(u => u.SisId.Equals(key, StringComparison.OrdinalIgnoreCase));

        var ok = user is not null &&
                 await Task.Run(() => PasswordHasher.Verify(password, user.PasswordHash, user.Salt));

        if (!ok)
        {
            var fails = (_attempts.TryGetValue(key, out var s) ? s.Fails : 0) + 1;
            _attempts[key] = fails >= MaxAttempts ? (0, DateTime.UtcNow.Add(LockDuration)) : (fails, null);
            return new AuthResult(false, Invalid, null);
        }

        _attempts.Remove(key);
        if (!user!.IsActive)
            return new AuthResult(false, "This account is inactive. Please contact your administrator.", null);

        return new AuthResult(true, "OK", user);
    }

    public async Task<AuthResult> RegisterAsync(RegistrationRequest req)
    {
        static AuthResult Fail(string msg) => new(false, msg, null);

        var name = req.FullName.Trim();
        var sis = req.SisId.Trim();

        if (name.Length < 3) return Fail("Enter your full name.");
        if (sis.Length < 4 || sis.Any(char.IsWhiteSpace)) return Fail("SIS ID must be at least 4 characters with no spaces.");
        if (!Enum.IsDefined(req.Role)) return Fail("Choose your role.");

        // School / department rules per role. Never trust the picker alone: validate against the catalog.
        string school, department;
        switch (req.Role)
        {
            case UserRole.Faculty:
            case UserRole.Chairperson:
                if (!AcademicCatalog.IsValidSchool(req.School)) return Fail("Choose your school.");
                if (!AcademicCatalog.IsValidDepartment(req.School, req.Department)) return Fail("Choose your department.");
                school = req.School; department = req.Department;
                break;
            case UserRole.Dean:
                if (!AcademicCatalog.IsValidSchool(req.School)) return Fail("Choose your school.");
                school = req.School; department = "";
                break;
            default: // READS Scholar, IMS Personnel: assigned to the IMS Office, no school or department yet
                school = ""; department = AcademicCatalog.ImsOffice;
                break;
        }
        if (req.Password.Length < 8 || !req.Password.Any(char.IsLetter) || !req.Password.Any(char.IsDigit))
            return Fail("Password needs at least 8 characters, with letters and numbers.");
        if (req.Password != req.ConfirmPassword) return Fail("Passwords do not match.");

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        if (users.Any(u => u.SisId.Equals(sis, StringComparison.OrdinalIgnoreCase)))
            return Fail("That SIS ID is already registered.");

        var (hash, salt) = await Task.Run(() => PasswordHasher.Hash(req.Password));
        var user = new UserAccount
        {
            Id = "U-" + Guid.NewGuid().ToString("N")[..8],
            SisId = sis, FullName = name, School = school, Department = department, Role = req.Role,
            PasswordHash = hash, Salt = salt
        };
        users.Add(user);
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        // Faculty also get a faculty record so the readiness check can link it.
        if (req.Role == UserRole.Faculty)
        {
            var faculty = await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile);
            faculty.Add(new FacultyMember
            {
                Id = "F-" + Guid.NewGuid().ToString("N")[..8],
                UserId = user.Id, FullName = name, School = school, Department = department
            });
            await _storage.SaveAsync(JsonStorageService.FacultyFile, faculty);
        }

        return new AuthResult(true, "Account created.", user);
    }
}
