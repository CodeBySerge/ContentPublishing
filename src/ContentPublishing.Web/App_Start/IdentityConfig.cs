using System;
using System.Configuration;
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
        }
    }
}
