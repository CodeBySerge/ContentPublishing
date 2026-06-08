using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContentPublishing.Web.Net8.Security;

public static class IdentitySeed
{
    private static readonly string[] DefaultRoles = ["Admin", "Reviewer", "Author"];

    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        catch
        {
            // Allow startup without Identity DB connectivity; auth routes will surface configuration issues.
            return;
        }

        foreach (var role in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}