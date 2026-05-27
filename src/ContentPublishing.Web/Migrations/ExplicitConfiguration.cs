using System.Data.Entity.Migrations;
using ContentPublishing.Web.Models;

namespace ContentPublishing.Web.Migrations
{
    internal sealed class ExplicitConfiguration : DbMigrationsConfiguration<ApplicationDbContext>
    {
        public ExplicitConfiguration()
        {
            AutomaticMigrationsEnabled = false;
            AutomaticMigrationDataLossAllowed = false;
            ContextKey = "ContentPublishing.Web.Models.ApplicationDbContext";
        }

        protected override void Seed(ApplicationDbContext context)
        {
        }
    }
}