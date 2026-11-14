namespace TextileConnect.Api;

public sealed record PincodeResponse(string Pincode, long CityId, string City, long StateId, string State);
