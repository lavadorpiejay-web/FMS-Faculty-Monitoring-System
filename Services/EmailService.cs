using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace FacultyMonitoring.Services;

/// <param name="Sent">The email really left the app (SMTP accepted it).</param>
/// <param name="Simulated">No mail account is set up, so nothing was sent; the caller shows the message on screen instead.</param>
public sealed record EmailSendResult(bool Sent, bool Simulated, string Message);

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(string to, string subject, string body);
}

/// <summary>Mail account settings. Read from Resources\Raw\emailsettings.json (kept out of GitHub, see emailsettings.sample.json).</summary>
public sealed class EmailSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string AppPassword { get; set; } = "";
    public string FromName { get; set; } = "FMS Faculty Monitoring System";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(AppPassword) && !AppPassword.StartsWith("PUT_", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Sends real email through SMTP when Resources\Raw\emailsettings.json is filled in.
/// Without it the app runs in "simulated email" mode: nothing is sent and the code is shown on screen,
/// so the whole flow can still be tested.
/// </summary>
public sealed class EmailService : IEmailService
{
    private EmailSettings? _settings;

    private async Task<EmailSettings> LoadSettingsAsync()
    {
        if (_settings is not null) return _settings;
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("emailsettings.json");
            _settings = await JsonSerializer.DeserializeAsync<EmailSettings>(stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new EmailSettings();
        }
        catch (Exception) { _settings = new EmailSettings(); }   // file missing: simulated mode
        return _settings;
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string body)
    {
        var s = await LoadSettingsAsync();
        if (!s.IsConfigured)
            return new EmailSendResult(false, true, $"To: {to}\nSubject: {subject}\n\n{body}");

        try
        {
#pragma warning disable SYSLIB0014
            using var client = new SmtpClient(s.Host, s.Port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(s.User, s.AppPassword),
                Timeout = 20000
            };
#pragma warning restore SYSLIB0014
            using var mail = new MailMessage { From = new MailAddress(s.User, s.FromName), Subject = subject, Body = body };
            mail.To.Add(to);
            await client.SendMailAsync(mail);
            return new EmailSendResult(true, false, "Email sent.");
        }
        catch (Exception)
        {
            return new EmailSendResult(false, false,
                "The email could not be sent. Check your internet connection and the mail settings, then try again.");
        }
    }
}
