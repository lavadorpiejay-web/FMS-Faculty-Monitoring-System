using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string sisId, string password);
    Task<AuthResult> RegisterAsync(RegistrationRequest request);

    /// <summary>Live check used by the Register screen: is this name or ID already registered?</summary>
    Task<DuplicateCheck> CheckDuplicatesAsync(string fullName, string sisId);

    /// <summary>Is the Chairperson (per department) or Dean (per school) seat already taken? Returns a warning, or "" if free.</summary>
    Task<string> CheckRoleOccupiedAsync(UserRole role, string? school, string? department);
}

public sealed record DuplicateCheck(bool NameTaken, bool SisTaken);


public sealed class LocalAuthService : IAuthService
{
    public const int MaxSisIdLength = 5;   // IDs are exactly 5 characters
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(1);
    private const string Invalid = "Invalid ID or password.";

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

    // Names match regardless of capitals or extra spaces ("juan  DELA cruz" = "Juan Dela Cruz").
    private static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public async Task<DuplicateCheck> CheckDuplicatesAsync(string fullName, string sisId)
    {
        var name = NormalizeName(fullName ?? "");
        var sis = (sisId ?? "").Trim();
        if (name.Length == 0 && sis.Length == 0) return new DuplicateCheck(false, false);

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return new DuplicateCheck(
            name.Length > 0 && users.Any(u => NormalizeName(u.FullName) == name),
            sis.Length > 0 && users.Any(u => u.SisId.Equals(sis, StringComparison.OrdinalIgnoreCase)));
    }

    public const string PasswordRule =
        "Password needs at least 8 characters with an uppercase letter, a lowercase letter, a number and a symbol.";

    // Mixed characters: upper + lower + digit + symbol.
    public static bool IsStrongPassword(string p) =>
        p.Length >= 8 && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit)
        && p.Any(c => !char.IsLetterOrDigit(c));

    private static string OccupiedMessage(List<UserAccount> users, UserRole role, string? school, string? department)
    {
        if (role == UserRole.Chairperson && !string.IsNullOrEmpty(department) &&
            users.Any(u => u.Role == UserRole.Chairperson && u.School == school && u.Department == department))
            return $"This role is occupied: the {department} already has a Chairperson.";
        if (role == UserRole.Dean && !string.IsNullOrEmpty(school) &&
            users.Any(u => u.Role == UserRole.Dean && u.School == school))
            return $"This role is occupied: the {school} already has a Dean.";
        return "";
    }

    public async Task<string> CheckRoleOccupiedAsync(UserRole role, string? school, string? department)
    {
        if (role is not (UserRole.Chairperson or UserRole.Dean)) return "";
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        return OccupiedMessage(users, role, school, department);
    }

    public async Task<AuthResult> RegisterAsync(RegistrationRequest req)
    {
        static AuthResult Fail(string msg) => new(false, msg, null);

        var name = req.FullName.Trim();
        var sis = req.SisId.Trim();

        if (name.Length < 3) return Fail("Enter your full name.");
        if (sis.Length != MaxSisIdLength || sis.Any(char.IsWhiteSpace)) return Fail($"ID must be exactly {MaxSisIdLength} characters with no spaces.");
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
        if (!IsStrongPassword(req.Password)) return Fail(PasswordRule);
        if (req.Password != req.ConfirmPassword) return Fail("Passwords do not match.");

        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        if (users.Any(u => u.SisId.Equals(sis, StringComparison.OrdinalIgnoreCase)))
            return Fail("That ID is already registered.");
        var occupied = OccupiedMessage(users, req.Role, school, department);
        if (occupied.Length > 0) return Fail(occupied);
        if (users.Any(u => NormalizeName(u.FullName) == NormalizeName(name)))
            return Fail("A user with this name is already registered.");

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
