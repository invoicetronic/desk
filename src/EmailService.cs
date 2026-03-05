using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Desk;

public class EmailService(DeskConfig config, ILogger<EmailService> logger)
{
    private readonly SmtpConfig _smtp = config.Smtp;

    public async Task SendSubscriptionWelcomeAsync(string userEmail)
    {
        await SendTemplateEmailAsync(
            userEmail,
            "Invoicetronic Desk — Abbonamento attivo / Subscription active",
            "SubscriptionWelcome",
            new Dictionary<string, string> { ["{{UserEmail}}"] = userEmail });
    }

    public async Task SendSubscriptionAdminNotifyAsync(string userEmail, string eventType, string status)
    {
        if (string.IsNullOrEmpty(_smtp.NotifyEmail))
            return;

        await SendTemplateEmailAsync(
            _smtp.NotifyEmail,
            $"Invoicetronic Desk — {eventType}",
            "SubscriptionNotifyAdmin",
            new Dictionary<string, string>
            {
                ["{{UserEmail}}"] = userEmail,
                ["{{EventType}}"] = eventType,
                ["{{SubscriptionStatus}}"] = status,
                ["{{EventDate}}"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            });
    }

    public async Task SendSubscriptionCanceledAsync(string userEmail)
    {
        await SendTemplateEmailAsync(
            userEmail,
            "Invoicetronic Desk — Abbonamento annullato / Subscription canceled",
            "SubscriptionCanceled",
            new Dictionary<string, string> { ["{{UserEmail}}"] = userEmail });
    }

    public async Task SendPaymentFailedAsync(string userEmail)
    {
        await SendTemplateEmailAsync(
            userEmail,
            "Invoicetronic Desk — Pagamento non riuscito / Payment failed",
            "SubscriptionPaymentFailed",
            new Dictionary<string, string> { ["{{UserEmail}}"] = userEmail });
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
            await client.ConnectAsync(_smtp.Host, _smtp.Port, socketOptions);

            if (!string.IsNullOrEmpty(_smtp.Username))
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
