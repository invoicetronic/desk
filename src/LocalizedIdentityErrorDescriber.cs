using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Desk;

public class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> L) : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = string.Format(L["Identity_DuplicateEmail"].Value, email)
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = string.Format(L["Identity_DuplicateUserName"].Value, userName)
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = string.Format(L["Identity_InvalidEmail"].Value, email)
    };

    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = string.Format(L["Identity_PasswordTooShort"].Value, length)
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = L["Identity_PasswordRequiresDigit"].Value
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = L["Identity_PasswordRequiresLower"].Value
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = L["Identity_PasswordRequiresUpper"].Value
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = L["Identity_PasswordRequiresNonAlphanumeric"].Value
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars),
        Description = string.Format(L["Identity_PasswordRequiresUniqueChars"].Value, uniqueChars)
    };

    public override IdentityError DefaultError() => new()
    {
        Code = nameof(DefaultError),
        Description = L["Identity_DefaultError"].Value
    };
}
