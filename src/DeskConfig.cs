using Microsoft.Extensions.Configuration;

namespace Desk;

public class DeskConfig
{
    [ConfigurationKeyName("api_url")]
    public string ApiUrl { get; set; } = "https://api.invoicetronic.com/v1";

    [ConfigurationKeyName("api_key")]
    public string? ApiKey { get; set; }

    public DatabaseConfig Database { get; set; } = new();
    public BrandingConfig Branding { get; set; } = new();
    public string? Locale { get; set; }

    public StripeConfig Stripe { get; set; } = new();
    public SmtpConfig Smtp { get; set; } = new();

    public bool IsStandalone => !string.IsNullOrEmpty(ApiKey);
    public bool IsBillingEnabled => !IsStandalone && Stripe.IsConfigured;
}

public class DatabaseConfig
{
    public string Provider { get; set; } = "sqlite";

    [ConfigurationKeyName("connection_string")]
    public string? ConnectionString { get; set; }
}

public class StripeConfig
{
    [ConfigurationKeyName("secret_key")]
    public string? SecretKey { get; set; }

    [ConfigurationKeyName("publishable_key")]
    public string? PublishableKey { get; set; }

    [ConfigurationKeyName("webhook_secret")]
    public string? WebhookSecret { get; set; }

    [ConfigurationKeyName("price_id_it")]
    public string? PriceIdIt { get; set; }

    [ConfigurationKeyName("price_id_foreign")]
    public string? PriceIdForeign { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(SecretKey) &&
        !string.IsNullOrEmpty(PublishableKey) &&
        !string.IsNullOrEmpty(WebhookSecret) &&
        !string.IsNullOrEmpty(PriceIdIt) &&
        !string.IsNullOrEmpty(PriceIdForeign);
}

public class SmtpConfig
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }

    [ConfigurationKeyName("sender_email")]
    public string? SenderEmail { get; set; }

    [ConfigurationKeyName("sender_name")]
    public string? SenderName { get; set; } = "Invoicetronic Desk";

    [ConfigurationKeyName("notify_email")]
    public string? NotifyEmail { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(Host) &&
        !string.IsNullOrEmpty(SenderEmail);
}

public class BrandingConfig
{
    [ConfigurationKeyName("app_name")]
    public string AppName { get; set; } = "Invoicetronic Desk";

    [ConfigurationKeyName("footer_text")]
    public string FooterText { get; set; } = "Powered by <a href=\"https://invoicetronic.com\">Invoicetronic</a>";

    [ConfigurationKeyName("logo_url")]
    public string? LogoUrl { get; set; }

    [ConfigurationKeyName("logo_dark_url")]
    public string? LogoDarkUrl { get; set; }

    [ConfigurationKeyName("favicon_url")]
    public string? FaviconUrl { get; set; }

    [ConfigurationKeyName("primary_color")]
    public string? PrimaryColor { get; set; }

    [ConfigurationKeyName("accent_color")]
    public string? AccentColor { get; set; }
}
