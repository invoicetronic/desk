using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace Desk;

public class BillingProfileModel : IValidatableObject
{
    public const string DefaultCountry = "IT";

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Profile_CompanyName")]
    public string CompanyName { get; set; } = "";

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Profile_Address")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Profile_City")]
    public string City { get; set; } = "";

    [Display(Name = "Profile_State")]
    public string? State { get; set; }

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Profile_ZipCode")]
    public string ZipCode { get; set; } = "";

    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Profile_Country")]
    public string Country { get; set; } = DefaultCountry;

    [Required(ErrorMessage = "Validation_Required")]
    [ItalianTaxId]
    [Display(Name = "Profile_TaxId")]
    public string TaxId { get; set; } = "";

    [Display(Name = "Profile_Pec")]
    public string? PecMail { get; set; }

    [Display(Name = "Profile_CodiceDestinatario")]
    public string? CodiceDestinatario { get; set; }

    [Display(Name = "Profile_PhoneNumber")]
    public string? PhoneNumber { get; set; }

    public bool IsTaxIdReadOnly { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Country == "IT" && string.IsNullOrWhiteSpace(PecMail) && string.IsNullOrWhiteSpace(CodiceDestinatario))
        {
            var localizer = validationContext.GetService<IStringLocalizer<SharedResource>>();
            yield return new ValidationResult(
                localizer?["Validation_RequireOne"] ?? "Validation_RequireOne",
                [nameof(PecMail), nameof(CodiceDestinatario)]);
        }
    }
}

public class ItalianTaxIdAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string taxId || string.IsNullOrWhiteSpace(taxId))
            return ValidationResult.Success;

        var model = validationContext.ObjectInstance as BillingProfileModel;
        if (model?.Country != "IT")
            return ValidationResult.Success;

        var localizer = validationContext.GetService<IStringLocalizer<SharedResource>>();

        if (!taxId.StartsWith("IT", StringComparison.OrdinalIgnoreCase))
            return new ValidationResult(localizer?["Validation_ItalianTaxIdFormat"] ?? "Validation_ItalianTaxIdFormat");

        if (taxId.Length != 13)
            return new ValidationResult(localizer?["Validation_ItalianTaxIdLength"] ?? "Validation_ItalianTaxIdLength");

        return ValidationResult.Success;
    }
}
