using System.Net.Mail;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using TextileConnect.Domain.Businesses;

namespace TextileConnect.Application.Businesses;

public static class BusinessInputRules
{
    public static bool IsPincode(string? value) => value is not null && Regex.IsMatch(value, "^[1-9][0-9]{5}$");
    public static bool IsGstin(string? value) => string.IsNullOrEmpty(value)
        || Regex.IsMatch(value, "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$");

    public static void ValidateProfile(string? displayName, string? email)
    {
        var errors = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200) errors.Add(new("displayName", "Enter a business name."));
        if (email is { Length: > 254 } || !string.IsNullOrWhiteSpace(email) && !MailAddress.TryCreate(email, out _)) errors.Add(new("email", "Enter a valid email."));
        if (errors.Count != 0) throw new ValidationException(errors);
    }

    public static void ValidateRegistration(Business business, string? pincode)
    {
        ValidateProfile(business.DisplayName, business.Email);
        var errors = new List<ValidationFailure>();
        if (business.BusinessType is not (BusinessType.Retailer or BusinessType.Manufacturer)) errors.Add(new("businessType", "Choose a business type."));
        if (string.IsNullOrWhiteSpace(business.LegalName) || business.LegalName.Length > 200) errors.Add(new("legalName", "Enter a legal name."));
        if (business.Phone is null || !Regex.IsMatch(business.Phone, "^\\+?[1-9][0-9]{7,14}$")) errors.Add(new("phone", "Enter a valid phone number."));
        if (business.OwnerName is { Length: > 120 }) errors.Add(new("ownerName", "Enter a name up to 120 characters."));
        if (!IsGstin(business.Gstin)) errors.Add(new("gstin", "Enter a 15-character GST number."));
        if (pincode is not null && !IsPincode(pincode)) errors.Add(new("pincode", "Enter a six-digit pincode."));
        if (errors.Count != 0) throw new ValidationException(errors);
    }
}
