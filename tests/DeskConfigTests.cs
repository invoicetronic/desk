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
}
