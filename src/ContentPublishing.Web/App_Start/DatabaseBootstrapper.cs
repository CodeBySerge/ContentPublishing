using System.Data.Entity;
using OneHealthHandbook.Web.Migrations;
using OneHealthHandbook.Web.Models;
using OneHealthHandbook.Web.Services;

namespace OneHealthHandbook.Web
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

