using TextileConnect.Domain.Businesses;

namespace TextileConnect.Application.Businesses;

public interface IBusinessDirectory
{
    Business Create(string userId, Business business, string? pincode = null);
    Business? GetForUser(string userId);
    Business? UpdateForUser(string userId, Action<Business> update);
    BusinessAddress AddAddress(string userId, BusinessAddress address);
    IReadOnlyList<BusinessAddress> ListAddresses(string userId);
    BusinessAddress? SetDefaultAddress(string userId, long addressId);
    IReadOnlyList<Business> ListPending();
    Business? SetVerification(long id, VerificationStatus status, string? note, string? approverId);
    BusinessUser AddStaff(string ownerUserId, string staffUserId);
}
