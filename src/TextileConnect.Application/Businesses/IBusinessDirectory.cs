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
}

public sealed class InMemoryBusinessDirectory : IBusinessDirectory
{
    private readonly List<(Business Business, string UserId, List<BusinessAddress> Addresses)> _records = [];
    public Business Create(string userId, Business business)
    {
        business.Id = _records.Count + 1;
        _records.Add((business, userId, []));
        return business;
    }
    public Business? GetForUser(string userId) => _records.FirstOrDefault(x => x.UserId == userId).Business;
    public Business? UpdateForUser(string userId, Action<Business> update)
    {
        var record = _records.FirstOrDefault(x => x.UserId == userId);
        if (record.Business is null) return null;
        update(record.Business);
        return record.Business;
    }
    public BusinessAddress AddAddress(string userId, BusinessAddress address)
    {
        var record = _records.First(x => x.UserId == userId);
        address.Id = record.Addresses.Count + 1;
        address.BusinessId = record.Business.Id;
        record.Addresses.Add(address);
        return address;
    }
    public IReadOnlyList<Business> ListPending() => _records.Where(x => x.Business.VerificationStatus == VerificationStatus.Pending).Select(x => x.Business).ToList();
    public Business? SetVerification(long id, VerificationStatus status, string? note, string? approverId)
    {
        var record = _records.FirstOrDefault(x => x.Business.Id == id);
        if (record.Business is null) return null;
        record.Business.VerificationStatus = status;
        record.Business.RejectionNote = note;
        record.Business.ApprovedByUserId = approverId;
        record.Business.ApprovedAtUtc = status == VerificationStatus.Approved ? DateTime.UtcNow : null;
        return record.Business;
    }
}
