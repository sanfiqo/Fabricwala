using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sanfiqo.Identity;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class BusinessApprovalHttpTests
{
    [Fact]
    public async Task Admin_queue_is_paged_and_review_does_not_expose_private_keys_or_full_phoneAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        using var another = await fixture.ClientAsync("Retailer", "another");
        using var admin = await fixture.ClientAsync("Admin", "admin");
        var id = await CreateAsync(owner);
        await CreateAsync(another);
        long documentId;
        string key;
        using (var scope = fixture.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            key = await storage.SavePrivateAsync(new MemoryStream(Encoding.UTF8.GetBytes("private document")), "document.txt");
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var document = new BusinessDocument { BusinessId = id, DocumentType = BusinessDocumentType.Gst, FileKey = key };
            db.BusinessDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/businesses?status=pending&page=1&pageSize=1");
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Single(list.GetProperty("items").EnumerateArray());
        var detail = await admin.GetAsync($"/api/v1/admin/businesses/{id}");
        var text = await detail.Content.ReadAsStringAsync();
        Assert.DoesNotContain(key, text);
        Assert.DoesNotContain("+919000000001", text);
        var review = JsonDocument.Parse(text).RootElement;
        Assert.Equal("not_checked", review.GetProperty("gstCheck").GetProperty("status").GetString());
        Assert.True(review.GetProperty("gstCheck").GetProperty("formatValid").GetBoolean());
        Assert.Equal("private document", await admin.GetStringAsync($"/api/v1/admin/businesses/{id}/documents/{documentId}"));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/businesses/{id + 1}/documents/{documentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/v1/admin/businesses/{id}/documents/{documentId}")).StatusCode);
    }

    [Fact]
    public async Task Approve_and_reject_refresh_all_members_and_require_a_noteAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        using var staffClient = await fixture.ClientAsync("Retailer", "staff");
        using var admin = await fixture.ClientAsync("Admin", "admin");
        var id = await CreateAsync(owner);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.BusinessUsers.Add(new BusinessUser { BusinessId = id, UserId = "staff", Role = BusinessMemberRole.Staff });
            await db.SaveChangesAsync();
            var directory = scope.ServiceProvider.GetRequiredService<IBusinessDirectory>();
            Assert.Throws<ValidationException>(() => directory.SetVerification(id, VerificationStatus.Rejected, " ", "admin"));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/v1/admin/businesses/{id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/businesses/{id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/businesses/{id}/approve", new { })).StatusCode);
        var stamps = new Dictionary<string, string?>();
        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var userId in new[] { "owner", "staff" })
            {
                var user = (await users.FindByIdAsync(userId))!;
                Assert.Contains("Manufacturer", await users.GetRolesAsync(user));
                Assert.Contains(await users.GetClaimsAsync(user), x => x.Type == BusinessClaimTypes.Approved && x.Value == "true");
                stamps[userId] = user.SecurityStamp;
            }
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/businesses/{id}/reject", new { note = " " })).StatusCode);
        var rejected = await admin.PostAsJsonAsync($"/api/v1/admin/businesses/{id}/reject", new { note = "  Update your document.  " });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Update your document.", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rejectionNote").GetString());
        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var userId in new[] { "owner", "staff" })
            {
                var user = (await users.FindByIdAsync(userId))!;
                Assert.DoesNotContain(await users.GetClaimsAsync(user), x => x.Type == BusinessClaimTypes.Approved);
                Assert.NotEqual(stamps[userId], user.SecurityStamp);
            }
            Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Notifications.CountAsync());
        }
    }

    [Fact]
    public async Task Approval_routes_enforce_roles_validation_and_not_foundAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var retailer = await fixture.ClientAsync("Retailer", "retailer");
        using var admin = await fixture.ClientAsync("Admin", "admin");
        foreach (var path in new[] { "/api/v1/admin/businesses", "/api/v1/admin/businesses/999" })
            Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.PostAsJsonAsync("/api/v1/admin/businesses/999/reject", new { note = "Invalid documents." })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/businesses/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/v1/admin/businesses/999/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/v1/admin/businesses/999/reject", new { note = "Invalid documents." })).StatusCode);
        foreach (var query in new[] { "status=invalid", "page=0", "pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/businesses?" + query)).StatusCode);
    }

    private static async Task<long> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Manufacturer", legalName = "Knits", displayName = "Knits", gstin = "27ABCDE1234F1Z0" });
        // Other businesses in the same test omit GST to preserve its uniqueness.
        if (response.StatusCode == HttpStatusCode.Conflict)
            response = await client.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Manufacturer", legalName = "Knits", displayName = "Knits" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
}
