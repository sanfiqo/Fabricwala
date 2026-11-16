namespace TextileConnect.Api;

public sealed record PincodeResponse(string Pincode, long CityId, string City, long StateId, string State);

public sealed record StateResponse(long Id, string Name, string Code);

public sealed record CityResponse(long Id, long StateId, string Name, bool IsDeliveryZone, bool IsSourcingHub);
