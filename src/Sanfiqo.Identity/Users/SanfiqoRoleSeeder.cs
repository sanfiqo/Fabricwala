using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Sanfiqo.Identity;

public static class SanfiqoRoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var roleName in new[] { SanfiqoRoleNames.Admin, SanfiqoRoleNames.Retailer, SanfiqoRoleNames.Manufacturer })
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(new ApplicationRole
            {
                Id = Guid.NewGuid().ToString("D"),
                Name = roleName
            });
            if (!result.Succeeded)
                throw new InvalidOperationException($"Unable to seed identity role '{roleName}'.");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
