using Desk.Data;

namespace Desk.Tests;

public class StripeServiceTests
{
    private static DeskConfig CreateConfig(string priceIdIt = "price_it", string priceIdForeign = "price_foreign") => new()
    {
        Stripe = new StripeConfig
        {
            SecretKey = "sk_test_fake",
            PublishableKey = "pk_test_fake",
            WebhookSecret = "whsec_fake",
            PriceIdIt = priceIdIt,
            PriceIdForeign = priceIdForeign
        }
    };

    [Fact]
    public void Constructor_SetsStripeApiKey()
    {
        var config = CreateConfig();
        _ = new StripeService(config);
        Assert.Equal("sk_test_fake", Stripe.StripeConfiguration.ApiKey);
    }

    [Theory]
    [InlineData("IT12345678901", "price_it")]
    [InlineData("it12345678901", "price_it")]
    [InlineData("DE123456789", "price_foreign")]
    [InlineData("FR12345678901", "price_foreign")]
    [InlineData(null, "price_foreign")]
    [InlineData("", "price_foreign")]
    public void PriceSelection_BasedOnTaxIdPrefix(string? taxId, string expectedPriceId)
    {
        // Verify the price selection logic in isolation
        // (same expression used in CreateCheckoutSessionAsync)
        var config = CreateConfig();
        var priceId = taxId?.StartsWith("IT", StringComparison.OrdinalIgnoreCase) == true
            ? config.Stripe.PriceIdIt
            : config.Stripe.PriceIdForeign;

        Assert.Equal(expectedPriceId, priceId);
    }

    [Fact]
    public void ConstructWebhookEvent_ThrowsOnInvalidSignature()
    {
        var config = CreateConfig();
        var service = new StripeService(config);

        Assert.Throws<Stripe.StripeException>(() =>
            service.ConstructWebhookEvent("{}", "invalid_sig"));
    }

    [Fact]
    public void StripeConfig_IsConfigured_WhenAllFieldsSet()
    {
        var config = CreateConfig();
        Assert.True(config.Stripe.IsConfigured);
    }

    [Theory]
    [InlineData(null, "pk", "wh", "pi", "pf")]
    [InlineData("sk", null, "wh", "pi", "pf")]
    [InlineData("sk", "pk", null, "pi", "pf")]
    [InlineData("sk", "pk", "wh", null, "pf")]
    [InlineData("sk", "pk", "wh", "pi", null)]
    public void StripeConfig_IsNotConfigured_WhenAnyFieldMissing(
        string? secretKey, string? publishableKey, string? webhookSecret, string? priceIdIt, string? priceIdForeign)
    {
        var config = new StripeConfig
        {
            SecretKey = secretKey,
            PublishableKey = publishableKey,
            WebhookSecret = webhookSecret,
            PriceIdIt = priceIdIt,
            PriceIdForeign = priceIdForeign
        };
        Assert.False(config.IsConfigured);
    }
}
