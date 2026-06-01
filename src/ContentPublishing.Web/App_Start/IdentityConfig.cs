using System;
using System.Configuration;
using System.Data.Entity;
using System.Net.Mail;
using System.Threading.Tasks;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Security;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;

namespace ContentPublishing.Web
{
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store)
            : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));

            manager.UserValidator = new UserValidator<ApplicationUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };

            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonLetterOrDigit = false
            };

            manager.EmailService = new SmtpEmailService();

            if (options.DataProtectionProvider != null)
            {
                var dataProtector = options.DataProtectionProvider.Create("ASP.NET Identity");
                manager.UserTokenProvider = new DataProtectorTokenProvider<ApplicationUser>(dataProtector);
            }

            return manager;
        }
    }

    public class ApplicationSignInManager : SignInManager<ApplicationUser, string>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager, Microsoft.Owin.Security.IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
        {
            return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
        }
    }

    public class SmtpEmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            var fromAddress = ConfigurationManager.AppSettings["smtpFromAddress"] ?? "noreply@contentpublishing.local";

            using (var smtp = new SmtpClient())
            using (var mail = new MailMessage(fromAddress, message.Destination)
            {
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = true
            })
            {
                return smtp.SendMailAsync(mail);
            }
        }
    }

    public static class IdentitySeeder
    {
        private sealed class SeedUserDefinition
        {
            public string Email { get; set; }
            public string FullName { get; set; }
            public string RoleName { get; set; }
            public string Description { get; set; }
            public string Password { get; set; }
        }

        public static async Task EnsureRolesAsync(ApplicationDbContext dbContext)
        {
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(dbContext));

            if (!await roleManager.RoleExistsAsync(RoleNames.Author))
            {
                await roleManager.CreateAsync(new IdentityRole(RoleNames.Author));
            }

            if (!await roleManager.RoleExistsAsync(RoleNames.Reviewer))
            {
                await roleManager.CreateAsync(new IdentityRole(RoleNames.Reviewer));
            }

            if (!await roleManager.RoleExistsAsync(RoleNames.Administrator))
            {
                await roleManager.CreateAsync(new IdentityRole(RoleNames.Administrator));
            }

            await dbContext.Database.ExecuteSqlCommandAsync(@"
IF COL_LENGTH('dbo.AspNetUsers', 'Description') IS NULL
    ALTER TABLE [dbo].[AspNetUsers] ADD [Description] NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.AspNetUsers', 'RoleId') IS NULL
    ALTER TABLE [dbo].[AspNetUsers] ADD [RoleId] NVARCHAR(128) NULL;

IF COL_LENGTH('dbo.AspNetRoles', 'Description') IS NULL
    ALTER TABLE [dbo].[AspNetRoles] ADD [Description] NVARCHAR(256) NULL;

IF COL_LENGTH('dbo.AspNetUserRoles', 'Description') IS NULL
    ALTER TABLE [dbo].[AspNetUserRoles] ADD [Description] NVARCHAR(256) NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_AspNetUsers_AspNetRoles_RoleId')
BEGIN
    ALTER TABLE [dbo].[AspNetUsers] WITH CHECK
    ADD CONSTRAINT [FK_AspNetUsers_AspNetRoles_RoleId]
    FOREIGN KEY([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_AspNetUsers_RoleId'
      AND object_id = OBJECT_ID('dbo.AspNetUsers'))
BEGIN
    CREATE INDEX [IX_AspNetUsers_RoleId] ON [dbo].[AspNetUsers]([RoleId]);
END;

IF COL_LENGTH('dbo.AspNetRoles', 'Description') IS NOT NULL
BEGIN
    EXEC('UPDATE [dbo].[AspNetRoles]
SET [Description] = CASE [Name]
    WHEN ''Author'' THEN ''Creates and submits draft content for review.''
    WHEN ''Reviewer'' THEN ''Reviews author submissions and approves or rejects with notes.''
    WHEN ''Administrator'' THEN ''Manages users, role assignments, and publication workflow.''
    ELSE [Description]
END;');
END;

IF COL_LENGTH('dbo.AspNetUserRoles', 'Description') IS NOT NULL AND COL_LENGTH('dbo.AspNetRoles', 'Description') IS NOT NULL
BEGIN
    EXEC('UPDATE ur
SET ur.[Description] = COALESCE(r.[Description], r.[Name])
FROM [dbo].[AspNetUserRoles] ur
INNER JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId];');
END;

UPDATE u
SET u.[RoleId] = ur.[RoleId]
FROM [dbo].[AspNetUsers] u
INNER JOIN [dbo].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
WHERE (u.[RoleId] IS NULL OR u.[RoleId] = '');

INSERT INTO [dbo].[AspNetUserRoles]([UserId], [RoleId])
SELECT u.[Id], u.[RoleId]
FROM [dbo].[AspNetUsers] u
WHERE u.[RoleId] IS NOT NULL
    AND u.[RoleId] <> ''
    AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[AspNetUserRoles] ur
            WHERE ur.[UserId] = u.[Id]
                AND ur.[RoleId] = u.[RoleId]);

UPDATE u
SET u.[Description] = COALESCE(NULLIF(u.[Description], ''), u.[FullName])
FROM [dbo].[AspNetUsers] u;");
        }

        public static async Task EnsureTestUsersAsync(ApplicationDbContext dbContext)
        {
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(dbContext));

            var roleIds = await dbContext.Roles
                .ToDictionaryAsync(r => r.Name, r => r.Id);

            var users = new[]
            {
                new SeedUserDefinition
                {
                    Email = "author.workflow@contentpublishing.local",
                    FullName = "Workflow Author",
                    RoleName = RoleNames.Author,
                    Description = "Seeded test account for author workflow validation.",
                    Password = "WorkflowAuthor1"
                },
                new SeedUserDefinition
                {
                    Email = "reviewer.workflow@contentpublishing.local",
                    FullName = "Workflow Reviewer",
                    RoleName = RoleNames.Reviewer,
                    Description = "Seeded test account for reviewer workflow validation.",
                    Password = "WorkflowReviewer1"
                },
                new SeedUserDefinition
                {
                    Email = "admin.workflow@contentpublishing.local",
                    FullName = "Workflow Admin",
                    RoleName = RoleNames.Administrator,
                    Description = "Seeded test account for admin queue workflow validation.",
                    Password = "WorkflowAdmin1"
                }
            };

            foreach (var def in users)
            {
                var user = await userManager.FindByEmailAsync(def.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = def.Email,
                        Email = def.Email,
                        FullName = def.FullName,
                        Description = def.Description,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    };

                    var createResult = await userManager.CreateAsync(user, def.Password);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException("Failed to seed test user " + def.Email + ": " + string.Join("; ", createResult.Errors));
                    }
                }

                if (!await userManager.IsInRoleAsync(user.Id, def.RoleName))
                {
                    var addRoleResult = await userManager.AddToRoleAsync(user.Id, def.RoleName);
                    if (!addRoleResult.Succeeded)
                    {
                        throw new InvalidOperationException("Failed to assign role " + def.RoleName + " to " + def.Email + ": " + string.Join("; ", addRoleResult.Errors));
                    }
                }

                var hasPassword = await userManager.HasPasswordAsync(user.Id);
                if (hasPassword)
                {
                    var removePasswordResult = await userManager.RemovePasswordAsync(user.Id);
                    if (!removePasswordResult.Succeeded)
                    {
                        throw new InvalidOperationException("Failed to reset password for " + def.Email + ": " + string.Join("; ", removePasswordResult.Errors));
                    }
                }

                var addPasswordResult = await userManager.AddPasswordAsync(user.Id, def.Password);
                if (!addPasswordResult.Succeeded)
                {
                    throw new InvalidOperationException("Failed to apply password for " + def.Email + ": " + string.Join("; ", addPasswordResult.Errors));
                }

                user.EmailConfirmed = true;
                user.IsActive = true;
                user.Description = def.Description;
                user.FullName = def.FullName;
                user.LastModifiedDate = DateTime.UtcNow;
                if (roleIds.ContainsKey(def.RoleName))
                {
                    user.RoleId = roleIds[def.RoleName];
                }

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException("Failed to update seeded user profile for " + def.Email + ": " + string.Join("; ", updateResult.Errors));
                }
            }
        }
    }
}
