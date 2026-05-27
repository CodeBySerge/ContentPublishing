namespace ContentPublishing.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AuditLog",
                c => new
                {
                    LogId = c.Guid(nullable: false),
                    UserId = c.String(maxLength: 128),
                    Action = c.String(nullable: false, maxLength: 50),
                    EntityType = c.String(nullable: false, maxLength: 50),
                    EntityId = c.Guid(nullable: false),
                    OldValue = c.String(),
                    NewValue = c.String(),
                    Timestamp = c.DateTime(nullable: false),
                    IpAddress = c.String(maxLength: 100),
                    ChangeDetails = c.String(),
                })
                .PrimaryKey(t => t.LogId);

            CreateTable(
                "dbo.Chapter",
                c => new
                {
                    ChapterId = c.Guid(nullable: false),
                    ContentId = c.Guid(nullable: false),
                    ChapterTitle = c.String(nullable: false, maxLength: 250),
                    ChapterBody = c.String(nullable: false),
                    ChapterOrder = c.Int(nullable: false),
                    CreatedDate = c.DateTime(nullable: false),
                    LastModifiedDate = c.DateTime(nullable: false),
                    IsDeleted = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.ChapterId)
                .ForeignKey("dbo.Content", t => t.ContentId, cascadeDelete: true)
                .Index(t => t.ContentId);

            CreateTable(
                "dbo.Content",
                c => new
                {
                    ContentId = c.Guid(nullable: false),
                    Title = c.String(nullable: false, maxLength: 250),
                    Description = c.String(),
                    Status = c.String(nullable: false, maxLength: 50),
                    AuthorId = c.String(nullable: false, maxLength: 128),
                    CreatedDate = c.DateTime(nullable: false),
                    LastModifiedDate = c.DateTime(nullable: false),
                    PublishedDate = c.DateTime(),
                    ScheduledPublishDate = c.DateTime(),
                    ArchivedDate = c.DateTime(),
                })
                .PrimaryKey(t => t.ContentId);

            CreateTable(
                "dbo.ContentImage",
                c => new
                {
                    ImageId = c.Guid(nullable: false),
                    ContentId = c.Guid(nullable: false),
                    FileName = c.String(nullable: false, maxLength: 260),
                    RelativePath = c.String(nullable: false, maxLength: 500),
                    ContentType = c.String(maxLength: 100),
                    CropX = c.Int(),
                    CropY = c.Int(),
                    CropWidth = c.Int(),
                    CropHeight = c.Int(),
                    IsPrimary = c.Boolean(nullable: false),
                    CreatedDate = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.ImageId)
                .ForeignKey("dbo.Content", t => t.ContentId)
                .Index(t => t.ContentId);

            CreateTable(
                "dbo.ContentVersion",
                c => new
                {
                    VersionId = c.Guid(nullable: false),
                    ContentId = c.Guid(nullable: false),
                    VersionNumber = c.Int(nullable: false),
                    Action = c.String(nullable: false, maxLength: 100),
                    CreatedByUserId = c.String(maxLength: 128),
                    CreatedDate = c.DateTime(nullable: false),
                    SnapshotJson = c.String(),
                    Notes = c.String(maxLength: 1000),
                })
                .PrimaryKey(t => t.VersionId)
                .ForeignKey("dbo.Content", t => t.ContentId)
                .Index(t => t.ContentId);

            CreateTable(
                "dbo.ContentReviewerAssignment",
                c => new
                {
                    AssignmentId = c.Guid(nullable: false),
                    ContentId = c.Guid(nullable: false),
                    ReviewerId = c.String(nullable: false, maxLength: 128),
                    AssignedByUserId = c.String(nullable: false, maxLength: 128),
                    AssignedDate = c.DateTime(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.AssignmentId)
                .ForeignKey("dbo.Content", t => t.ContentId)
                .ForeignKey("dbo.AspNetUsers", t => t.ReviewerId)
                .Index(t => t.ContentId)
                .Index(t => t.ReviewerId);

            CreateTable(
                "dbo.AspNetUsers",
                c => new
                {
                    Id = c.String(nullable: false, maxLength: 128),
                    FullName = c.String(),
                    IsActive = c.Boolean(nullable: false),
                    CreatedDate = c.DateTime(nullable: false),
                    LastModifiedDate = c.DateTime(nullable: false),
                    LastLogin = c.DateTime(),
                    Email = c.String(maxLength: 256),
                    EmailConfirmed = c.Boolean(nullable: false),
                    PasswordHash = c.String(),
                    SecurityStamp = c.String(),
                    PhoneNumber = c.String(),
                    PhoneNumberConfirmed = c.Boolean(nullable: false),
                    TwoFactorEnabled = c.Boolean(nullable: false),
                    LockoutEndDateUtc = c.DateTime(),
                    LockoutEnabled = c.Boolean(nullable: false),
                    AccessFailedCount = c.Int(nullable: false),
                    UserName = c.String(nullable: false, maxLength: 256),
                })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");

            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    UserId = c.String(nullable: false, maxLength: 128),
                    ClaimType = c.String(),
                    ClaimValue = c.String(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                {
                    LoginProvider = c.String(nullable: false, maxLength: 128),
                    ProviderKey = c.String(nullable: false, maxLength: 128),
                    UserId = c.String(nullable: false, maxLength: 128),
                })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                {
                    UserId = c.String(nullable: false, maxLength: 128),
                    RoleId = c.String(nullable: false, maxLength: 128),
                })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);

            CreateTable(
                "dbo.Review",
                c => new
                {
                    ReviewId = c.Guid(nullable: false),
                    ContentId = c.Guid(nullable: false),
                    ReviewerId = c.String(nullable: false, maxLength: 128),
                    Status = c.String(nullable: false, maxLength: 50),
                    Comments = c.String(),
                    SubmittedDate = c.DateTime(nullable: false),
                    ReviewDate = c.DateTime(),
                })
                .PrimaryKey(t => t.ReviewId)
                .ForeignKey("dbo.Content", t => t.ContentId)
                .ForeignKey("dbo.AspNetUsers", t => t.ReviewerId)
                .Index(t => t.ContentId)
                .Index(t => t.ReviewerId);

            CreateTable(
                "dbo.AspNetRoles",
                c => new
                {
                    Id = c.String(nullable: false, maxLength: 128),
                    Name = c.String(nullable: false, maxLength: 256),
                })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
        }

        public override void Down()
        {
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Review", "ReviewerId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Review", "ContentId", "dbo.Content");
            DropForeignKey("dbo.ContentReviewerAssignment", "ReviewerId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ContentReviewerAssignment", "ContentId", "dbo.Content");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ContentVersion", "ContentId", "dbo.Content");
            DropForeignKey("dbo.ContentImage", "ContentId", "dbo.Content");
            DropForeignKey("dbo.Chapter", "ContentId", "dbo.Content");
            DropIndex("dbo.AspNetRoles", new[] { "Name" });
            DropIndex("dbo.Review", new[] { "ReviewerId" });
            DropIndex("dbo.Review", new[] { "ContentId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.ContentReviewerAssignment", new[] { "ReviewerId" });
            DropIndex("dbo.ContentReviewerAssignment", new[] { "ContentId" });
            DropIndex("dbo.ContentVersion", new[] { "ContentId" });
            DropIndex("dbo.ContentImage", new[] { "ContentId" });
            DropIndex("dbo.Chapter", new[] { "ContentId" });
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.Review");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.ContentReviewerAssignment");
            DropTable("dbo.ContentVersion");
            DropTable("dbo.ContentImage");
            DropTable("dbo.Content");
            DropTable("dbo.Chapter");
            DropTable("dbo.AuditLog");
        }
    }
}
