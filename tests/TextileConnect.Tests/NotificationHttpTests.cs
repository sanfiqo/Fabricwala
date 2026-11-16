using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Notifications;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class NotificationHttpTests
{
    [Fact]
    public async Task Notifications_list_count_and_mark_read_are_per_user_and_pagedAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapNotificationEndpoints());
        var baseTime = DateTime.UtcNow;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            for (var i = 1; i <= 5; i++) db.Notifications.Add(new Notification { UserId = "user-a", Title = $"A{i}", Message = "m", Link = "/x", CreatedAtUtc = baseTime.AddMinutes(i) });
            db.Notifications.Add(new Notification { UserId = "user-b", Title = "B1", Message = "m" });
            await db.SaveChangesAsync();
        }
        using var a = await fixture.ClientAsync("Retailer", "user-a", 30);
        using var b = await fixture.ClientAsync("Manufacturer", "user-b", 10);
        Assert.Equal(5, (await a.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32());
        Assert.Equal(1, (await b.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32());

        var first = await a.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=2");
        Assert.Equal(5, first.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "A5", "A4" }, first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("title").GetString()).ToArray()); // newest first
        var third = await a.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=2&page=3");
        Assert.Equal("A1", third.GetProperty("items")[0].GetProperty("title").GetString());
        foreach (var bad in new[] { "?page=0", "?pageSize=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/v1/notifications" + bad)).StatusCode);

        var id = first.GetProperty("items")[0].GetProperty("id").GetInt64();
        var read = await a.PostAsync($"/api/v1/notifications/{id}/read", null);
        Assert.True((await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isRead").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsync($"/api/v1/notifications/{id}/read", null)).StatusCode); // repeat is harmless
        Assert.Equal(4, (await a.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32());
        Assert.Equal(4, (await a.GetFromJsonAsync<JsonElement>("/api/v1/notifications?unread=true")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/v1/notifications/{id}/read", null)).StatusCode); // someone else's
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync("/api/v1/notifications/99999/read", null)).StatusCode);

        var all = await a.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(4, (await all.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("updated").GetInt32());
        Assert.Equal(0, (await a.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32());
        Assert.Equal(1, (await b.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32()); // b untouched
        Assert.Equal(0, (await (await a.PostAsync("/api/v1/notifications/read-all", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("updated").GetInt32());
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
    }
}
