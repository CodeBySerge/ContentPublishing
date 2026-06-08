using ContentPublishing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContentPublishing.Web.Net8.Data;

public sealed class ContentReadDbContext : DbContext
{
    public ContentReadDbContext(DbContextOptions<ContentReadDbContext> options)
        : base(options)
    {
    }

    public DbSet<ContentItem> Contents => Set<ContentItem>();
    public DbSet<Chapter> Chapters => Set<Chapter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContentItem>().ToTable("Content");
        modelBuilder.Entity<Chapter>().ToTable("Chapter");
    }
}