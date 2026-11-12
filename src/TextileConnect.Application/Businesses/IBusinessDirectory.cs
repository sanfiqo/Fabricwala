using TextileConnect.Domain.Businesses;

namespace TextileConnect.Application.Businesses;

public interface IBusinessDirectory
{
    Business Create(string userId, Business business);
    Business? GetForUser(string userId);
    Business? UpdateForUser(string userId, Action<Business> update);
    BusinessAddress AddAddress(string userId, BusinessAddress address);
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
}
