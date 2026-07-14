using System;
using System.Data.Entity;
using OneHealthHandbook.Web.Models;
using OneHealthHandbook.Web;

class Program {
    static void Main() {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<ApplicationDbContext, OneHealthHandbook.Web.Migrations.Configuration>());
        using (var ctx = ApplicationDbContext.Create()) {
            ctx.Database.Initialize(false);
            IdentitySeeder.EnsureRolesAsync(ctx).GetAwaiter().GetResult();
            IdentitySeeder.EnsureTestUsersAsync(ctx).GetAwaiter().GetResult();
            Console.WriteLine('SEED_OK');
        }
    }
}
