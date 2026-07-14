using System.Data.Entity.Migrations;
using OneHealthHandbook.Web.Models;

namespace OneHealthHandbook.Web.Migrations
{
    internal sealed class ExplicitConfiguration : DbMigrationsConfiguration<ApplicationDbContext>
    {
        public ExplicitConfiguration()
        {
            AutomaticMigrationsEnabled = false;
            AutomaticMigrationDataLossAllowed = false;
            ContextKey = "OneHealthHandbook.Web.Models.ApplicationDbContext";
        }

        protected override void Seed(ApplicationDbContext context)
        {
        }
    }
}
