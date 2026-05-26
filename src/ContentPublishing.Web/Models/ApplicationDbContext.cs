using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;

namespace ContentPublishing.Web.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("BibleVerseDB", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        public DbSet<ContentEntity> Contents { get; set; }
        public DbSet<ChapterEntity> Chapters { get; set; }
        public DbSet<ReviewEntity> Reviews { get; set; }
        public DbSet<ContentReviewerAssignmentEntity> ContentReviewerAssignments { get; set; }
        public DbSet<AuditLogEntity> AuditLogs { get; set; }
        public DbSet<ContentImageEntity> ContentImages { get; set; }
        public DbSet<ContentVersionEntity> ContentVersions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");
            modelBuilder.Entity<ContentEntity>().ToTable("Content");
            modelBuilder.Entity<ChapterEntity>().ToTable("Chapter");
            modelBuilder.Entity<ReviewEntity>().ToTable("Review");
            modelBuilder.Entity<ContentReviewerAssignmentEntity>().ToTable("ContentReviewerAssignment");
            modelBuilder.Entity<AuditLogEntity>().ToTable("AuditLog");
            modelBuilder.Entity<ContentImageEntity>().ToTable("ContentImage");
            modelBuilder.Entity<ContentVersionEntity>().ToTable("ContentVersion");

            modelBuilder.Entity<ChapterEntity>()
                .HasRequired(ch => ch.Content)
                .WithMany(c => c.Chapters)
                .HasForeignKey(ch => ch.ContentId);

            modelBuilder.Entity<ReviewEntity>()
                .HasRequired(r => r.Content)
                .WithMany()
                .HasForeignKey(r => r.ContentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ReviewEntity>()
                .HasRequired(r => r.Reviewer)
                .WithMany()
                .HasForeignKey(r => r.ReviewerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ContentReviewerAssignmentEntity>()
                .HasRequired(a => a.Content)
                .WithMany()
                .HasForeignKey(a => a.ContentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ContentReviewerAssignmentEntity>()
                .HasRequired(a => a.Reviewer)
                .WithMany()
                .HasForeignKey(a => a.ReviewerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ContentImageEntity>()
                .HasRequired(i => i.Content)
                .WithMany(c => c.Images)
                .HasForeignKey(i => i.ContentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ContentVersionEntity>()
                .HasRequired(v => v.Content)
                .WithMany(c => c.Versions)
                .HasForeignKey(v => v.ContentId)
                .WillCascadeOnDelete(false);
        }
    }
}
