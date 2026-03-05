namespace Desk.Tests;

public class DeskConfigTests
{
    [Fact]
    public void IsStandalone_ReturnsTrue_WhenApiKeySet()
    {
        var config = new DeskConfig { ApiKey = "itk_live_abc123" };
        Assert.True(config.IsStandalone);
    }

    [Fact]
    public void IsStandalone_ReturnsFalse_WhenApiKeyNull()
    {
        var config = new DeskConfig { ApiKey = null };
        Assert.False(config.IsStandalone);
    }

    [Fact]
    public void IsStandalone_ReturnsFalse_WhenApiKeyEmpty()
    {
        var config = new DeskConfig { ApiKey = "" };
        Assert.False(config.IsStandalone);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var config = new DeskConfig();

        Assert.Equal("https://api.invoicetronic.com/v1", config.ApiUrl);
        Assert.Null(config.ApiKey);
        Assert.Equal("sqlite", config.Database.Provider);
        Assert.Null(config.Locale);
        Assert.Equal("Invoicetronic Desk", config.Branding.AppName);
        Assert.Contains("Invoicetronic", config.Branding.FooterText);
    }

    [Fact]
    public void IsBillingEnabled_ReturnsFalse_WhenStandalone()
    {
        var config = new DeskConfig
        {
            ApiKey = "itk_live_abc123",
            Stripe = new StripeConfig
            {
                SecretKey = "sk_test_xxx",
                PublishableKey = "pk_test_xxx",
                WebhookSecret = "whsec_xxx",
                PriceIdIt = "price_it_xxx",
                PriceIdForeign = "price_foreign_xxx"
            }
        };
        Assert.False(config.IsBillingEnabled);
    }

    [Fact]
    public void IsBillingEnabled_ReturnsFalse_WhenStripeNotConfigured()
    {
        var config = new DeskConfig();
        Assert.False(config.IsBillingEnabled);
    }

    [Fact]
    public void IsBillingEnabled_ReturnsFalse_WhenStripePartiallyConfigured()
    {
        var config = new DeskConfig
        {
            Stripe = new StripeConfig
            {
                SecretKey = "sk_test_xxx",
                PublishableKey = "pk_test_xxx"
            }
        };
        Assert.False(config.IsBillingEnabled);
    }

    [Fact]
    public void IsBillingEnabled_ReturnsTrue_WhenMultiUserAndStripeFullyConfigured()
    {
        var config = new DeskConfig
        {
            Stripe = new StripeConfig
            {
                SecretKey = "sk_test_xxx",
                PublishableKey = "pk_test_xxx",
                WebhookSecret = "whsec_xxx",
                PriceIdIt = "price_it_xxx",
                PriceIdForeign = "price_foreign_xxx"
            }
        };
        Assert.True(config.IsBillingEnabled);
    }

    [Fact]
    public void StripeConfig_IsConfigured_ReturnsFalse_WhenDefault()
    {
        var stripe = new StripeConfig();
        Assert.False(stripe.IsConfigured);
    }

    [Fact]
    public void SmtpConfig_IsConfigured_ReturnsFalse_WhenDefault()
    {
        var smtp = new SmtpConfig();
        Assert.False(smtp.IsConfigured);
    }

    [Fact]
    public void SmtpConfig_IsConfigured_ReturnsFalse_WhenOnlyHostSet()
    {
        var smtp = new SmtpConfig { Host = "smtp.example.com" };
        Assert.False(smtp.IsConfigured);
    }

    [Fact]
    public void SmtpConfig_IsConfigured_ReturnsTrue_WhenHostAndSenderEmailSet()
    {
        var smtp = new SmtpConfig { Host = "smtp.example.com", SenderEmail = "noreply@example.com" };
        Assert.True(smtp.IsConfigured);
    }

    [Fact]
    public void SmtpConfig_Defaults_AreCorrect()
    {
        var smtp = new SmtpConfig();
        Assert.Equal(587, smtp.Port);
        Assert.Equal("Invoicetronic Desk", smtp.SenderName);
        Assert.Null(smtp.NotifyEmail);
    }
}
