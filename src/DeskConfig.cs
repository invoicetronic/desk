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

    public bool IsStandalone => !string.IsNullOrEmpty(ApiKey);
}

public class DatabaseConfig
{
    public string Provider { get; set; } = "sqlite";

    [ConfigurationKeyName("connection_string")]
    public string? ConnectionString { get; set; }
}

public class BrandingConfig
{
    [ConfigurationKeyName("app_name")]
    public string AppName { get; set; } = "Invoicetronic Desk";

    [ConfigurationKeyName("footer_text")]
    public string FooterText { get; set; } = "Powered by <a href=\"https://invoicetronic.com\">Invoicetronic</a>";
}
