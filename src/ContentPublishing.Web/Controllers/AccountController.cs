using System.Web.Mvc;
using System.Threading.Tasks;
using System.Web;
using System.Linq;
using System.Data.Entity;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Security;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;

namespace ContentPublishing.Web.Controllers
{
    public class AccountController : Controller
    {
        private ApplicationUserManager _userManager;
        private ApplicationSignInManager _signInManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set => _userManager = value;
        }

        public ApplicationSignInManager SignInManager
        {
            get => _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            private set => _signInManager = value;
        }

        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;

        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Description = model.Description,
                IsActive = true
            };

            var result = await UserManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                var addRoleResult = await UserManager.AddToRoleAsync(user.Id, RoleNames.Author);
                if (!addRoleResult.Succeeded)
                {
                    await UserManager.DeleteAsync(user);
                    AddErrors(addRoleResult);
                    return View(model);
                }

                var dbContext = HttpContext.GetOwinContext().Get<ApplicationDbContext>();
                var authorRoleId = await dbContext.Roles
                    .Where(r => r.Name == RoleNames.Author)
                    .Select(r => r.Id)
                    .SingleOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(authorRoleId))
                {
                    user.RoleId = authorRoleId;
                    user.LastModifiedDate = System.DateTime.UtcNow;
                    await UserManager.UpdateAsync(user);
                }

                var code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code }, protocol: Request.Url.Scheme);

                try
                {
                    await UserManager.SendEmailAsync(
                        user.Id,
                        "Confirm your account",
                        $"Please confirm your account by clicking <a href=\"{callbackUrl}\">this link</a>.");
                }
                catch
                {
                    TempData["RegistrationWarning"] = "Your account was created, but we could not send a confirmation email right now. Please contact support to activate your account.";
                }

                return View("RegistrationPending");
            }

            AddErrors(result);
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account is deactivated. Contact an administrator.");
                return View(model);
            }

            if (!await UserManager.IsEmailConfirmedAsync(user.Id))
            {
                ModelState.AddModelError(string.Empty, "You must confirm your email before signing in.");
                return View(model);
            }

            var result = await SignInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    user.LastLogin = System.DateTime.UtcNow;
                    user.LastModifiedDate = System.DateTime.UtcNow;
                    await UserManager.UpdateAsync(user);
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                default:
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
            }
        }

        [Authorize]
        [ActionName("Profile")]
        public async Task<ActionResult> UserProfile()
        {
            var userId = User.Identity.GetUserId();
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            string roleName = null;
            if (!string.IsNullOrWhiteSpace(user.RoleId))
            {
                roleName = await HttpContext.GetOwinContext().Get<ApplicationDbContext>().Roles
                    .Where(r => r.Id == user.RoleId)
                    .Select(r => r.Name)
                    .SingleOrDefaultAsync();
            }

            if (string.IsNullOrWhiteSpace(roleName))
            {
                var roles = await UserManager.GetRolesAsync(userId);
                roleName = GetRoleDisplay(roles, returnRawRoleName: true);
            }

            ViewBag.RoleDisplay = ToDisplayRole(roleName);

            return View(user);
        }

        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                return View("Error");
            }

            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public ActionResult AccessDenied()
        {
            return View();
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        private static string GetRoleDisplay(System.Collections.Generic.IEnumerable<string> roles, bool returnRawRoleName = false)
        {
            if (roles == null)
            {
                return returnRawRoleName ? null : "Unassigned";
            }

            foreach (var role in roles)
            {
                if (role == "Administrator")
                {
                    return returnRawRoleName ? "Administrator" : "Admin";
                }
            }

            foreach (var role in roles)
            {
                if (role == "Reviewer")
                {
                    return returnRawRoleName ? "Reviewer" : "Reviewer";
                }
            }

            foreach (var role in roles)
            {
                if (role == "Author")
                {
                    return returnRawRoleName ? "Author" : "Author";
                }
            }

            return returnRawRoleName ? null : "Unassigned";
        }

        private static string ToDisplayRole(string roleName)
        {
            if (roleName == "Administrator")
            {
                return "Admin";
            }

            if (roleName == "Reviewer")
            {
                return "Reviewer";
            }

            if (roleName == "Author")
            {
                return "Author";
            }

            return "Unassigned";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _userManager?.Dispose();
                _signInManager?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
