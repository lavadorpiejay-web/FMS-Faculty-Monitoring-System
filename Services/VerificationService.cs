using System.Security.Cryptography;
using System.Text;
using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <param name="Simulated">No mail account is set up: show <paramref name="SimulatedMail"/> on screen instead of an inbox.</param>
public sealed record CodeSendResult(bool Success, string Message, bool Simulated = false, string SimulatedMail = "");

/// <summary>One-time 6-digit codes sent by email: confirming a new account and resetting a forgotten password.</summary>
public sealed class VerificationService
{
    private const int CodeMinutes = 10;
    private const int ResendSeconds = 30;
    private const int MaxAttempts = 5;

    private readonly JsonStorageService _storage;
    private readonly IEmailService _email;
    private readonly IAuthService _auth;

    public VerificationService(JsonStorageService storage, IEmailService email, IAuthService auth)
    {
        _storage = storage; _email = email; _auth = auth;
    }

    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at < 2) return email;
        return email[0] + new string('*', Math.Max(1, at - 2)) + email[(at - 1)..];
    }

    private static string Hash(string userId, CodePurpose purpose, string code) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}|{purpose}|{code}")));

    private async Task<List<UserAccount>> UsersAsync()
    {
        await _storage.InitializeAsync();
        return await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
    }

    public async Task<UserAccount?> FindUserAsync(string userId) =>
        (await UsersAsync()).FirstOrDefault(u => u.Id == userId);

    /// <summary>Creates a fresh code for this account, stores its hash and emails it.</summary>
    private async Task<CodeSendResult> SendAsync(UserAccount user, CodePurpose purpose)
    {
        var codes = await _storage.LoadAsync<VerificationCode>(JsonStorageService.VerificationsFile);
        codes.RemoveAll(c => c.ExpiresUtc < DateTime.UtcNow);                 // tidy old codes

        var previous = codes.FirstOrDefault(c => c.UserId == user.Id && c.Purpose == purpose);
        if (previous is not null)
        {
            var wait = (int)Math.Ceiling((previous.SentUtc.AddSeconds(ResendSeconds) - DateTime.UtcNow).TotalSeconds);
            if (wait > 0) return new CodeSendResult(false, $"Please wait {wait}s before asking for another code.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        codes.RemoveAll(c => c.UserId == user.Id && c.Purpose == purpose);
        codes.Add(new VerificationCode
        {
            UserId = user.Id, Purpose = purpose, CodeHash = Hash(user.Id, purpose, code),
            SentUtc = DateTime.UtcNow, ExpiresUtc = DateTime.UtcNow.AddMinutes(CodeMinutes)
        });
        await _storage.SaveAsync(JsonStorageService.VerificationsFile, codes);

        var (subject, intro) = purpose == CodePurpose.VerifyEmail
            ? ("FMS: confirm your email", "Use this code to confirm your email address and finish creating your FMS account.")
            : ("FMS: reset your password", "Use this code to reset your FMS password.");
        var body = $"Hello {user.FullName},\n\n{intro}\n\nYour code: {code}\n\n" +
                   $"It expires in {CodeMinutes} minutes. If you did not ask for this, you can ignore this email.\n\nFMS Faculty Monitoring System";

        var sent = await _email.SendAsync(user.Email, subject, body);
        if (sent.Simulated)
            return new CodeSendResult(true, "Simulated email: no mail account is set up, so nothing was really sent.", true, sent.Message);
        return sent.Sent
            ? new CodeSendResult(true, $"We sent a 6-digit code to {MaskEmail(user.Email)}.")
            : new CodeSendResult(false, sent.Message);
    }

    /// <summary>True when the code matches. Wrong guesses are counted; too many burn the code.</summary>
    private async Task<(bool Ok, string Message)> ConfirmAsync(string userId, CodePurpose purpose, string code)
    {
        code = (code ?? "").Trim();
        var codes = await _storage.LoadAsync<VerificationCode>(JsonStorageService.VerificationsFile);
        var record = codes.FirstOrDefault(c => c.UserId == userId && c.Purpose == purpose);
        if (record is null) return (false, "No code was requested. Ask for a new code.");
        if (record.ExpiresUtc < DateTime.UtcNow)
        {
            codes.Remove(record);
            await _storage.SaveAsync(JsonStorageService.VerificationsFile, codes);
            return (false, "That code has expired. Ask for a new code.");
        }
        if (record.Attempts >= MaxAttempts) return (false, "Too many wrong codes. Ask for a new code.");

        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(record.CodeHash), Encoding.UTF8.GetBytes(Hash(userId, purpose, code)));
        if (!ok)
        {
            codes[codes.IndexOf(record)] = record with { Attempts = record.Attempts + 1 };
            await _storage.SaveAsync(JsonStorageService.VerificationsFile, codes);
            return (false, "That code is not correct.");
        }
        codes.Remove(record);
        await _storage.SaveAsync(JsonStorageService.VerificationsFile, codes);
        return (true, "OK");
    }

    // ---- Confirming a new account's email ----

    public async Task<CodeSendResult> SendRegistrationCodeAsync(string userId)
    {
        var user = await FindUserAsync(userId);
        if (user is null) return new CodeSendResult(false, "Account not found.");
        if (user.EmailVerified) return new CodeSendResult(false, "This email is already verified.");
        return await SendAsync(user, CodePurpose.VerifyEmail);
    }

    public async Task<AuthResult> VerifyEmailAsync(string userId, string code)
    {
        var users = await UsersAsync();
        var user = users.FirstOrDefault(u => u.Id == userId);
        if (user is null) return new AuthResult(false, "Account not found.", null, AuthIssue.Invalid);
        if (user.EmailVerified) return new AuthResult(true, "Your email is already verified. You can sign in.", user);

        var (ok, msg) = await ConfirmAsync(userId, CodePurpose.VerifyEmail, code);
        if (!ok) return new AuthResult(false, msg, null, AuthIssue.Invalid);

        var updated = user with { EmailVerified = true };
        users[users.IndexOf(user)] = updated;
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        var approver = LocalAuthService.ApproverOf(user.Role);
        var message = user.Status == ApprovalStatus.Pending && approver is not null
            ? $"Email verified. Your account now waits for approval from your {RolePolicy.Describe(approver.Value).DisplayName}. You can sign in once it is approved."
            : "Email verified. You can now sign in.";
        return new AuthResult(true, message, updated);
    }

    // ---- Forgot password ----

    private static string GenericResetMessage =>
        "If that email address is registered, a 6-digit code has been sent to it.";

    /// <summary>Always answers the same way, so nobody can find out which emails are registered.</summary>
    public async Task<CodeSendResult> SendResetCodeAsync(string email)
    {
        var mail = LocalAuthService.NormalizeEmail(email);
        if (!LocalAuthService.IsValidEmail(mail)) return new CodeSendResult(false, "Enter a valid email address.");

        var user = (await UsersAsync()).FirstOrDefault(u =>
            LocalAuthService.NormalizeEmail(u.Email) == mail && u.Status != ApprovalStatus.Declined);
        if (user is null) return new CodeSendResult(true, GenericResetMessage);

        var result = await SendAsync(user, CodePurpose.ResetPassword);
        // Keep the wording identical on success; report only real failures (cooldown, mail error).
        return result.Success && !result.Simulated ? new CodeSendResult(true, GenericResetMessage) : result;
    }

    public async Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword, string confirm)
    {
        static AuthResult Fail(string msg) => new(false, msg, null, AuthIssue.Invalid);

        // Check the new password first so a typo does not use up a code attempt.
        if (!LocalAuthService.IsStrongPassword(newPassword)) return Fail(LocalAuthService.PasswordRule);
        if (newPassword != confirm) return Fail("Passwords do not match.");

        var mail = LocalAuthService.NormalizeEmail(email);
        var users = await UsersAsync();
        var user = users.FirstOrDefault(u =>
            LocalAuthService.NormalizeEmail(u.Email) == mail && u.Status != ApprovalStatus.Declined);
        if (user is null) return Fail("That code is not correct.");

        var (ok, msg) = await ConfirmAsync(user.Id, CodePurpose.ResetPassword, code);
        if (!ok) return Fail(msg);

        var (hash, salt) = await Task.Run(() => PasswordHasher.Hash(newPassword));
        // Getting the code in the mailbox also proves the email address is theirs.
        users[users.IndexOf(user)] = user with { PasswordHash = hash, Salt = salt, EmailVerified = true };
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);
        _auth.ClearLockout(user.SisId);
        return new AuthResult(true, "Your password was changed. You can sign in with the new password.", null);
    }
}
