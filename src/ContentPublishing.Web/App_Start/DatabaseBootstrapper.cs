using System.Data.Entity;
using ContentPublishing.Web.Migrations;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Services;

namespace ContentPublishing.Web
{
    public static class DatabaseBootstrapper
    {
        public static void Initialize()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<ApplicationDbContext, Configuration>());

            using (var context = ApplicationDbContext.Create())
            {
                context.Database.Initialize(false);
                IdentitySeeder.EnsureRolesAsync(context).GetAwaiter().GetResult();
                IdentitySeeder.EnsureTestUsersAsync(context).GetAwaiter().GetResult();
                HandbookImportService.EnsureImported(context);
            }
        }
    }
}
