using System.Net;
using System.Net.Http.Json;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class LocationListHttpTests
{
    [Fact]
    public async Task States_and_cities_are_public_sorted_and_filterable_by_stateAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var anonymous = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };

        var states = await anonymous.GetFromJsonAsync<List<StateResponse>>("/api/v1/states");
        Assert.NotNull(states);
        Assert.NotEmpty(states);
        Assert.Equal(states.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Id), states.Select(x => x.Id));
        var punjab = states.Single(x => x.Name == "Punjab");

        var all = await anonymous.GetFromJsonAsync<List<CityResponse>>("/api/v1/cities");
        Assert.Contains(all!, x => x.Name == "Ludhiana");

        var inPunjab = await anonymous.GetFromJsonAsync<List<CityResponse>>($"/api/v1/cities?stateId={punjab.Id}");
        Assert.NotEmpty(inPunjab!);
        Assert.All(inPunjab!, x => Assert.Equal(punjab.Id, x.StateId));
        Assert.Contains(inPunjab!, x => x.Name == "Ludhiana");
        Assert.True(inPunjab!.Count < all!.Count);

        Assert.Empty((await anonymous.GetFromJsonAsync<List<CityResponse>>("/api/v1/cities?stateId=999999"))!);        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/v1/cities?stateId=abc")).StatusCode);
    }
}
