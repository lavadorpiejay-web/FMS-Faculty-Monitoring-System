using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FacultyMonitoring.Services;

/// <param name="Sent">The email really left the app (the mail server accepted it).</param>
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
/// Sends real email through SMTP (MailKit) when Resources\Raw\emailsettings.json is filled in.
/// It tries the configured port first (587 = STARTTLS) and, if that fails, port 465 (SSL from the start),
/// because some networks only let one of them through.
/// Without the settings file the app runs in "simulated email" mode: nothing is sent and the code is shown on screen,
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

    private static async Task SendOnceAsync(EmailSettings s, int port, SecureSocketOptions security, MimeMessage message)
    {
        // Phones and some networks cannot reach the certificate revocation (OCSP) servers, which makes the handshake fail
        // even though the certificate is valid. Everything else about the certificate is still checked.
        using var client = new SmtpClient { Timeout = 20000, CheckCertificateRevocation = false };
        await client.ConnectAsync(s.Host, port, security);
        await client.AuthenticateAsync(s.User, s.AppPassword.Replace(" ", ""));
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task<EmailSendResult> SendAsync(string to, string subject, string body)
    {
        var s = await LoadSettingsAsync();
        if (!s.IsConfigured)
            return new EmailSendResult(false, true, $"To: {to}\nSubject: {subject}\n\n{body}");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(s.FromName, s.User));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        string first, second = "";
        try
        {
            await SendOnceAsync(s, s.Port, s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, message);
            return new EmailSendResult(true, false, "Email sent.");
        }
        catch (Exception ex) { first = $"port {s.Port}: {ex.GetType().Name}: {ex.Message}"; }

        // A wrong password will not get better on another port, but a blocked TLS handshake can.
        if (s.Port != 465)
        {
            try
            {
                await SendOnceAsync(s, 465, SecureSocketOptions.SslOnConnect, message);
                return new EmailSendResult(true, false, "Email sent.");
            }
            catch (Exception ex) { second = $"port 465: {ex.GetType().Name}: {ex.Message}"; }
        }

        return new EmailSendResult(false, false,
            "The email could not be sent. Check your internet connection and the mail settings, then try again.\n\nReason:\n" +
            first + (second.Length > 0 ? "\n" + second : ""));
    }
}
