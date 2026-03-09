using Microsoft.Extensions.Localization;
using Moq;

namespace Desk.Tests;

public class LocalizedIdentityErrorDescriberTests
{
    private static readonly Dictionary<string, string> Templates = new()
    {
        ["Identity_DuplicateEmail"] = "Email '{0}' already registered.",
        ["Identity_DuplicateUserName"] = "Username '{0}' already taken.",
        ["Identity_InvalidEmail"] = "Email '{0}' is not valid.",
        ["Identity_PasswordTooShort"] = "Password must be at least {0} characters.",
        ["Identity_PasswordRequiresDigit"] = "Password requires a digit.",
        ["Identity_PasswordRequiresLower"] = "Password requires a lowercase letter.",
        ["Identity_PasswordRequiresUpper"] = "Password requires an uppercase letter.",
        ["Identity_PasswordRequiresNonAlphanumeric"] = "Password requires a special character.",
        ["Identity_PasswordRequiresUniqueChars"] = "Password must have at least {0} unique characters.",
        ["Identity_DefaultError"] = "An error occurred."
    };

    private static LocalizedIdentityErrorDescriber CreateDescriber()
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();

        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, Templates.GetValueOrDefault(key, key)));

        return new LocalizedIdentityErrorDescriber(localizer.Object);
    }

    [Fact]
    public void DuplicateEmail_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.DuplicateEmail("test@test.com");
        Assert.Equal("DuplicateEmail", error.Code);
        Assert.Contains("test@test.com", error.Description);
    }

    [Fact]
    public void DuplicateUserName_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.DuplicateUserName("testuser");
        Assert.Equal("DuplicateUserName", error.Code);
        Assert.Contains("testuser", error.Description);
    }

    [Fact]
    public void InvalidEmail_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.InvalidEmail("bad@email");
        Assert.Equal("InvalidEmail", error.Code);
        Assert.Contains("bad@email", error.Description);
    }

    [Fact]
    public void PasswordTooShort_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.PasswordTooShort(8);
        Assert.Equal("PasswordTooShort", error.Code);
        Assert.Contains("8", error.Description);
    }

    [Fact]
    public void PasswordRequiresDigit_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.PasswordRequiresDigit();
        Assert.Equal("PasswordRequiresDigit", error.Code);
    }

    [Fact]
    public void PasswordRequiresLower_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.PasswordRequiresLower();
        Assert.Equal("PasswordRequiresLower", error.Code);
    }

    [Fact]
    public void PasswordRequiresUpper_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.PasswordRequiresUpper();
        Assert.Equal("PasswordRequiresUpper", error.Code);
    }

    [Fact]
    public void PasswordRequiresNonAlphanumeric_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.PasswordRequiresNonAlphanumeric();
        Assert.Equal("PasswordRequiresNonAlphanumeric", error.Code);
    }

    [Fact]
    public void PasswordRequiresUniqueChars_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.PasswordRequiresUniqueChars(4);
        Assert.Equal("PasswordRequiresUniqueChars", error.Code);
        Assert.Contains("4", error.Description);
    }

    [Fact]
    public void DefaultError_ReturnsCorrectCode()
    {
        var describer = CreateDescriber();
        var error = describer.DefaultError();
        Assert.Equal("DefaultError", error.Code);
    }
}
