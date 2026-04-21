using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Desk;

public class EmailService(DeskConfig config, ILogger<EmailService> logger)
{
    private readonly SmtpConfig _smtp = config.Smtp;

    public async Task SendPasswordResetAsync(string userEmail, string resetLink)
    {
        await SendTemplateEmailAsync(
            userEmail,
            "Invoicetronic Desk — Reimposta password / Reset password",
            "PasswordReset",
            new Dictionary<string, string>
            {
                ["{{UserEmail}}"] = userEmail,
                ["{{ResetLink}}"] = resetLink
            });
    }

    public virtual async Task SendRegistrationAdminNotifyAsync(string userEmail, string displayName)
    {
        if (string.IsNullOrEmpty(_smtp.NotifyEmail))
            return;

        await SendTemplateEmailAsync(
            _smtp.NotifyEmail,
            "Invoicetronic Desk — New user registered",
            "RegistrationNotifyAdmin",
            new Dictionary<string, string>
            {
                ["{{UserEmail}}"] = userEmail,
                ["{{DisplayName}}"] = displayName,
                ["{{EventDate}}"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            });
    }

    private async Task SendTemplateEmailAsync(string to, string subject, string templateName, Dictionary<string, string> replacements)
    {
        if (!_smtp.IsConfigured)
            return;

        try
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Artifacts", "MailTemplates", $"{templateName}.html");
            var body = await File.ReadAllTextAsync(templatePath);

            foreach (var (placeholder, value) in replacements)
                body = body.Replace(placeholder, value);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtp.SenderName, _smtp.SenderEmail!));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            var socketOptions = _smtp.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_smtp.Host!, _smtp.Port, socketOptions);

            if (!string.IsNullOrEmpty(_smtp.Username) && !string.IsNullOrEmpty(_smtp.Password))
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }
}
