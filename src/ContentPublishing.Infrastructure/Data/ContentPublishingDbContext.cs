using System.Data.Entity;
using ContentPublishing.Domain.Entities;

namespace ContentPublishing.Infrastructure.Data
{
    public class ContentPublishingDbContext : DbContext
    {
        public ContentPublishingDbContext()
            : base("name=ContentPublishingDb")
        {
        }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<ContentItem> Contents { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ContentReviewerAssignment> ContentReviewerAssignments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>().ToTable("Users");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<ContentItem>().ToTable("Content");
            modelBuilder.Entity<Chapter>().ToTable("Chapter");
            modelBuilder.Entity<Review>().ToTable("Review");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLog");
            modelBuilder.Entity<ContentReviewerAssignment>().ToTable("ContentReviewerAssignment");
        }
    }
}
