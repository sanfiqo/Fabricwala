using FluentValidation;
using FluentValidation.Results;
using TextileConnect.Domain.Businesses;

namespace TextileConnect.Application.Businesses;

public static class BusinessVerificationRules
{
    public static void Validate(VerificationStatus status, string? note)
    {
        if (status is not (VerificationStatus.Approved or VerificationStatus.Rejected))
            throw new ValidationException([new ValidationFailure("status", "Choose approve or reject.")]);
        if (status == VerificationStatus.Rejected && (string.IsNullOrWhiteSpace(note) || note.Length > 1000))
            throw new ValidationException([new ValidationFailure("note", "Enter a rejection note of up to 1000 characters.")]);
    }
}
