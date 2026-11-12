using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;

namespace TextileConnect.Api;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/businesses").RequireAuthorization();
        group.MapPost("", (Business business, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.Create(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", business)));
        group.MapGet("/me", (ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.GetForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "") is { } business
                ? Results.Ok(business) : Results.NotFound());
        group.MapPut("/me", (BusinessUpdate request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.UpdateForUser(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", b =>
            { b.DisplayName = request.DisplayName; b.Email = request.Email; }) is { } business
                ? Results.Ok(business) : Results.NotFound());
        group.MapPost("/me/addresses", (BusinessAddress address, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.AddAddress(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", address)));
        group.MapPost("/me/users", (StaffRequest request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            Results.Ok(directory.AddStaff(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", request.UserId)));

        var admin = app.MapGroup("/api/v1/admin/businesses").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("", (IBusinessDirectory directory) => Results.Ok(directory.ListPending()));
        admin.MapPost("/{id:long}/approve", (long id, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.SetVerification(id, VerificationStatus.Approved, null, principal.FindFirstValue(ClaimTypes.NameIdentifier)) is { } business
                ? Results.Ok(business) : Results.NotFound());
        admin.MapPost("/{id:long}/reject", (long id, Rejection request, ClaimsPrincipal principal, IBusinessDirectory directory) =>
            directory.SetVerification(id, VerificationStatus.Rejected, request.Note, principal.FindFirstValue(ClaimTypes.NameIdentifier)) is { } business
                ? Results.Ok(business) : Results.NotFound());
    }

    public sealed record BusinessUpdate(string DisplayName, string? Email);
    public sealed record Rejection(string Note);
    public sealed record StaffRequest(string UserId);
}
