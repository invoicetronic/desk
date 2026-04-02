using Microsoft.Extensions.Logging.Abstractions;

namespace Desk.Tests;

public class EmailServiceTests
{
    [Fact]
    public async Task SendPasswordResetAsync_DoesNothing_WhenSmtpNotConfigured()
    {
        var config = new DeskConfig { Smtp = new SmtpConfig() };
        var service = new EmailService(config, NullLogger<EmailService>.Instance);

        // Should complete without error (early return)
        await service.SendPasswordResetAsync("user@test.com", "https://reset-link");
    }

    [Fact]
    public void SmtpConfig_IsConfigured_WhenHostAndSenderSet()
    {
        var config = new SmtpConfig { Host = "smtp.test.com", SenderEmail = "noreply@test.com" };
        Assert.True(config.IsConfigured);
    }

    [Theory]
    [InlineData(null, "noreply@test.com")]
    [InlineData("", "noreply@test.com")]
    [InlineData("smtp.test.com", null)]
    [InlineData("smtp.test.com", "")]
    [InlineData(null, null)]
    public void SmtpConfig_IsNotConfigured_WhenHostOrSenderMissing(string? host, string? senderEmail)
    {
        var config = new SmtpConfig { Host = host, SenderEmail = senderEmail };
        Assert.False(config.IsConfigured);
    }
}
