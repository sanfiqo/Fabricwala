using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Businesses;

public sealed class EfBusinessDirectory(MarketplaceDbContext db) : IBusinessDirectory
{
    public Business Create(string userId, Business business, string? pincode = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException();
        business = new Business
        {
            BusinessType = business.BusinessType, LegalName = business.LegalName?.Trim() ?? string.Empty,
            DisplayName = business.DisplayName?.Trim() ?? string.Empty, Phone = business.Phone?.Trim() ?? string.Empty,
            Email = string.IsNullOrWhiteSpace(business.Email) ? null : business.Email.Trim(),
            Gstin = string.IsNullOrWhiteSpace(business.Gstin) ? null : business.Gstin.Trim().ToUpperInvariant()
        };
        BusinessInputRules.ValidateRegistration(business, pincode);
        if (db.BusinessUsers.Any(x => x.UserId == userId)
            || business.Gstin != null && db.Businesses.Any(x => x.Gstin == business.Gstin)) throw new BusinessConflictException();
        var postal = pincode is null ? null : db.PostalCodes.SingleOrDefault(x => x.Pincode == pincode)
            ?? throw new KeyNotFoundException();
        using var transaction = db.Database.IsRelational() ? db.Database.BeginTransaction() : null;
        db.Businesses.Add(business);
        db.SaveChanges();
        db.BusinessUsers.Add(new BusinessUser { BusinessId = business.Id, UserId = userId, Role = BusinessMemberRole.Owner, IsPrimary = true });
        if (postal is not null) db.BusinessAddresses.Add(new BusinessAddress
        {
            BusinessId = business.Id, AddressType = AddressType.Registered, Line1 = string.Empty,
            CityId = postal.CityId, Pincode = postal.Pincode, IsDefault = true
        });
        db.SaveChanges();
        transaction?.Commit();
        return business;
    }
    public Business? GetForUser(string userId) => db.BusinessUsers.Where(x => x.UserId == userId).Join(db.Businesses, x => x.BusinessId, x => x.Id, (_, business) => business).SingleOrDefault();
    public Business? UpdateForUser(string userId, Action<Business> update)
    {
        var business = GetForUser(userId);
        if (business is null || business.IsDeleted) return null;
        update(business);
        BusinessInputRules.ValidateProfile(business.DisplayName, business.Email);
        business.UpdatedAtUtc = DateTime.UtcNow;
        db.SaveChanges();
        return business;
    }
    public BusinessAddress AddAddress(string userId, BusinessAddress address)
    {
        var business = GetForUser(userId) ?? throw new InvalidOperationException("Business context is required.");
        address.BusinessId = business.Id;
        if (!db.BusinessAddresses.Any(x => x.BusinessId == business.Id && x.AddressType == address.AddressType)) address.IsDefault = true;
        if (address.IsDefault) ClearDefaults(business.Id, address.AddressType);
        db.BusinessAddresses.Add(address); db.SaveChanges(); return address;
    }
    public IReadOnlyList<BusinessAddress> ListAddresses(string userId) => GetForUser(userId) is { } business
        ? db.BusinessAddresses.Where(x => x.BusinessId == business.Id).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).ToList()
        : Array.Empty<BusinessAddress>();
    public BusinessAddress? SetDefaultAddress(string userId, long addressId)
    {
        var business = GetForUser(userId);
        var address = business is null ? null : db.BusinessAddresses.SingleOrDefault(x => x.Id == addressId && x.BusinessId == business.Id);
        if (address is null) return null;
        ClearDefaults(address.BusinessId, address.AddressType); address.IsDefault = true; db.SaveChanges(); return address;
    }
    private void ClearDefaults(long businessId, AddressType type)
    {
        foreach (var address in db.BusinessAddresses.Where(x => x.BusinessId == businessId && x.AddressType == type && x.IsDefault)) address.IsDefault = false;
    }
    public IReadOnlyList<Business> ListPending() => db.Businesses.Where(x => x.VerificationStatus == VerificationStatus.Pending && !x.IsDeleted).ToList();
    public Business? SetVerification(long id, VerificationStatus status, string? note, string? approverId) { var business = db.Businesses.SingleOrDefault(x => x.Id == id); if (business is null) return null; business.VerificationStatus = status; business.RejectionNote = note; business.ApprovedByUserId = approverId; business.ApprovedAtUtc = status == VerificationStatus.Approved ? DateTime.UtcNow : null; db.SaveChanges(); return business; }
    public BusinessUser AddStaff(string ownerUserId, string staffUserId) { var business = GetForUser(ownerUserId) ?? throw new InvalidOperationException("Business context is required."); var member = new BusinessUser { BusinessId = business.Id, UserId = staffUserId, Role = BusinessMemberRole.Staff }; db.BusinessUsers.Add(member); db.SaveChanges(); return member; }
}
