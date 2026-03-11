using Microsoft.AspNetCore.DataProtection;

namespace Desk;

public class ApiKeyProtector
{
    private const string Prefix = "ENC:";
    private readonly IDataProtector _protector;

    public ApiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Desk.ApiKey");
    }

    public string Protect(string plainText)
    {
        return Prefix + _protector.Protect(plainText);
    }

    public string Unprotect(string value)
    {
        if (value.StartsWith(Prefix))
            return _protector.Unprotect(value[Prefix.Length..]);

        // Plaintext (not yet migrated)
        return value;
    }

    public string? UnprotectOrNull(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : Unprotect(value);
    }

    public bool IsEncrypted(string value) => value.StartsWith(Prefix);
}
