using TextileConnect.Domain.Businesses;

namespace TextileConnect.Application.Businesses;

public interface IBusinessDirectory
{
    Business Create(string userId, Business business);
    Business? GetForUser(string userId);
    Business? UpdateForUser(string userId, Action<Business> update);
    BusinessAddress AddAddress(string userId, BusinessAddress address);
    IReadOnlyList<Business> ListPending();
    Business? SetVerification(long id, VerificationStatus status, string? note, string? approverId);
    BusinessUser AddStaff(string ownerUserId, string staffUserId);
}
