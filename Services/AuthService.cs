using System.Net.Mail;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string sisId, string password);
    Task<AuthResult> RegisterAsync(RegistrationRequest request);

    /// <summary>Live check used by the Register screen: is this name, ID or email already registered?</summary>
    Task<DuplicateCheck> CheckDuplicatesAsync(string fullName, string sisId, string email);

    /// <summary>Is the Chairperson (per department) or Dean (per school) seat already taken? Returns a warning, or "" if free.</summary>
    Task<string> CheckRoleOccupiedAsync(UserRole role, string? school, string? department);

    /// <summary>Forget the failed sign-in attempts of an ID (used after a password reset).</summary>
    void ClearLockout(string sisId);
}

public sealed record DuplicateCheck(bool NameTaken, bool SisTaken, bool EmailTaken);


public sealed class LocalAuthService : IAuthService
{
    /// <summary>Employee IDs (Faculty, Chairperson, Dean, IMS Personnel) are exactly this long.</summary>
    public const int EmployeeIdLength = 5;
    /// <summary>READS Scholars are students, so their student ID may be longer: up to this many characters.</summary>
    public const int ScholarIdMaxLength = 10;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(1);
    private const string Invalid = "Invalid ID or password.";

    public static int MaxIdLength(UserRole? role) => role == UserRole.ReadsScholar ? ScholarIdMaxLength : EmployeeIdLength;

    public static string IdRule(UserRole? role) => role == UserRole.ReadsScholar
        ? $"Student ID: {EmployeeIdLength} to {ScholarIdMaxLength} characters, no spaces."
        : $"Employee ID: exactly {EmployeeIdLength} characters, no spaces.";

    /// <summary>Who has to approve a new account of this role (null: nobody, it is approved automatically).</summary>
    /// <summary>Who approves an account of this role and school. The Chairperson of the Center for Religious Education (no school, so no Dean) is approved by IMS Personnel.</summary>
    public static UserRole? ApproverOf(UserRole role, string? school) =>
        role == UserRole.Chairperson && AcademicCatalog.IsIndependent(school) ? UserRole.ImsPersonnel : ApproverOf(role);

    public static UserRole? ApproverOf(UserRole role) => role switch
    {
        UserRole.Faculty => UserRole.Chairperson,
        UserRole.Chairperson => UserRole.Dean,
        UserRole.ReadsScholar => UserRole.ImsPersonnel,
        _ => null
    };

    private readonly JsonStorageService _storage;
    private readonly Dictionary<string, (int Fails, DateTime? LockedUntil)> _attempts = new();

    public LocalAuthService(JsonStorageService storage) => _storage = storage;

    public void ClearLockout(string sisId) => _attempts.Remove((sisId ?? "").Trim().ToLowerInvariant());

    public async Task<AuthResult> SignInAsync(string sisId, string password)
    {
        var key = sisId.Trim().ToLowerInvariant();

        if (_attempts.TryGetValue(key, out var state) && state.LockedUntil is { } until && until > DateTime.UtcNow)
        {
            var secs = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds);
            return new AuthResult(false, $"Too many attempts. Try again in {secs}s.", null, AuthIssue.Locked);
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
            return new AuthResult(false, Invalid, null, AuthIssue.Invalid);
        }

        _attempts.Remove(key);
        if (!user!.IsActive)
            return new AuthResult(false, "This account is inactive. Please contact your administrator.", null, AuthIssue.Inactive);

        // The password is right, so it is safe to say what is still missing.
        if (!user.EmailVerified)
            return new AuthResult(false, "Your email is not verified yet. Enter the code we sent you.", user, AuthIssue.EmailNotVerified);

        var approver = ApproverOf(user.Role, user.School);
        if (user.Status == ApprovalStatus.Pending)
            return new AuthResult(false,
                $"Your account is waiting for approval from your {RolePolicy.Describe(approver ?? user.Role).DisplayName}. You can sign in once it is approved.",
                null, AuthIssue.PendingApproval);
        if (user.Status == ApprovalStatus.Declined)
            return new AuthResult(false,
                $"Your registration was declined by your {RolePolicy.Describe(approver ?? user.Role).DisplayName}. You can register again.",
                null, AuthIssue.Declined);

        return new AuthResult(true, "OK", user);
    }

    // Names match regardless of capitals or extra spaces ("juan  DELA cruz" = "Juan Dela Cruz").
    private static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email)
    {
        if (email.Length is < 5 or > 120 || email.Any(char.IsWhiteSpace)) return false;
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email && addr.Host.Contains('.');
        }
        catch (FormatException) { return false; }
    }

    // A declined registration does not block the person from registering again.
    private static IEnumerable<UserAccount> Live(IEnumerable<UserAccount> users) =>
        users.Where(u => u.Status != ApprovalStatus.Declined);

    public async Task<DuplicateCheck> CheckDuplicatesAsync(string fullName, string sisId, string email)
    {
        var name = NormalizeName(fullName ?? "");
        var sis = (sisId ?? "").Trim();
        var mail = NormalizeEmail(email);
        if (name.Length == 0 && sis.Length == 0 && mail.Length == 0) return new DuplicateCheck(false, false, false);

        await _storage.InitializeAsync();
        var users = Live(await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile)).ToList();
        return new DuplicateCheck(
            name.Length > 0 && users.Any(u => NormalizeName(u.FullName) == name),
            sis.Length > 0 && users.Any(u => u.SisId.Equals(sis, StringComparison.OrdinalIgnoreCase)),
            mail.Length > 0 && users.Any(u => NormalizeEmail(u.Email) == mail));
    }

    public const string PasswordRule =
        "Password needs at least 8 characters with an uppercase letter, a lowercase letter, a number and a symbol.";

    // Mixed characters: upper + lower + digit + symbol.
    public static bool IsStrongPassword(string p) =>
        p.Length >= 8 && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit)
        && p.Any(c => !char.IsLetterOrDigit(c));

    // Pending Chairpersons and Deans still hold their seat; only a declined one frees it.
    private static string OccupiedMessage(IEnumerable<UserAccount> users, UserRole role, string? school, string? department)
    {
        var live = Live(users).ToList();
        if (role == UserRole.Chairperson && !string.IsNullOrEmpty(department) &&
            live.Any(u => u.Role == UserRole.Chairperson && u.School == school && u.Department == department))
            return $"This role is occupied: the {department} already has a Chairperson.";
        if (role == UserRole.Dean && !string.IsNullOrEmpty(school) &&
            live.Any(u => u.Role == UserRole.Dean && u.School == school))
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
        static AuthResult Fail(string msg) => new(false, msg, null, AuthIssue.Invalid);

        var name = req.FullName.Trim();
        var sis = req.SisId.Trim();
        var email = NormalizeEmail(req.Email);

        if (name.Length < 3) return Fail("Enter your full name.");
        if (!Enum.IsDefined(req.Role)) return Fail("Choose your role.");

        var maxId = MaxIdLength(req.Role);
        const int minId = EmployeeIdLength;
        if (sis.Length < minId || sis.Length > maxId || sis.Any(char.IsWhiteSpace)) return Fail(IdRule(req.Role));
        if (!IsValidEmail(email)) return Fail("Enter a valid email address. The verification code is sent there.");

        // School / department rules per role. Never trust the picker alone: validate against the catalog.
        string school, department;
        var ownSchool = "";
        switch (req.Role)
        {
            case UserRole.Faculty:
            case UserRole.Chairperson:
                if (!AcademicCatalog.IsValidUnit(req.School)) return Fail("Choose your school (or the Center for Religious Education).");
                if (!AcademicCatalog.IsValidDepartment(req.School, req.Department)) return Fail("Choose your department.");
                school = req.School; department = req.Department;
                break;
            case UserRole.Dean:
                if (!AcademicCatalog.IsValidSchool(req.School)) return Fail("Choose your school.");
                school = req.School; department = "";
                break;
            case UserRole.ReadsScholar:   // placed in the IMS Office; the school they study in is kept as OwnSchool
                if (!AcademicCatalog.IsValidSchool(req.School)) return Fail("Choose the school you study in.");
                ownSchool = req.School; school = ""; department = AcademicCatalog.ImsOffice;
                break;
            default: // IMS Personnel: assigned to the IMS Office, no school or department
                school = ""; department = AcademicCatalog.ImsOffice;
                break;
        }
        if (!IsStrongPassword(req.Password)) return Fail(PasswordRule);
        if (req.Password != req.ConfirmPassword) return Fail("Passwords do not match.");

        await _storage.InitializeAsync();
        var all = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);

        // Someone whose earlier registration was declined may register again: drop the old declined record.
        var declined = all.Where(u => u.Status == ApprovalStatus.Declined &&
            (u.SisId.Equals(sis, StringComparison.OrdinalIgnoreCase) || NormalizeEmail(u.Email) == email ||
             NormalizeName(u.FullName) == NormalizeName(name))).Select(u => u.Id).ToHashSet();
        var users = all.Where(u => !declined.Contains(u.Id)).ToList();

        if (users.Any(u => u.SisId.Equals(sis, StringComparison.OrdinalIgnoreCase)))
            return Fail("That ID is already registered.");
        if (users.Any(u => NormalizeEmail(u.Email) == email))
            return Fail("That email address is already registered.");
        if (users.Any(u => NormalizeName(u.FullName) == NormalizeName(name)))
            return Fail("A user with this name is already registered.");
        var occupied = OccupiedMessage(users, req.Role, school, department);
        if (occupied.Length > 0) return Fail(occupied);

        var (hash, salt) = await Task.Run(() => PasswordHasher.Hash(req.Password));
        var user = new UserAccount
        {
            Id = "U-" + Guid.NewGuid().ToString("N")[..8],
            SisId = sis, FullName = name, Email = email, School = school, Department = department, Role = req.Role, OwnSchool = ownSchool,
            EmailVerified = false,
            Status = ApproverOf(req.Role, school) is null ? ApprovalStatus.Approved : ApprovalStatus.Pending,
            PasswordHash = hash, Salt = salt
        };
        users.Add(user);
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        var faculty = await _storage.LoadAsync<FacultyMember>(JsonStorageService.FacultyFile);
        var removed = faculty.RemoveAll(f => f.UserId is not null && declined.Contains(f.UserId));

        // Faculty also get a faculty record so the readiness check can link it.
        if (req.Role == UserRole.Faculty)
        {
            faculty.Add(new FacultyMember
            {
                Id = "F-" + Guid.NewGuid().ToString("N")[..8],
                UserId = user.Id, FullName = name, School = school, Department = department, Email = email
            });
            removed++;
        }
        if (removed > 0) await _storage.SaveAsync(JsonStorageService.FacultyFile, faculty);

        return new AuthResult(true, "Account created.", user);
    }
}
