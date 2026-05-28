using System.Web.Mvc;
using System.Threading.Tasks;
using System.Web;
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
                IsActive = true
            };

            var result = await UserManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                var roleResult = await UserManager.AddToRoleAsync(user.Id, RoleNames.Author);
                if (!roleResult.Succeeded)
                {
                    AddErrors(roleResult);
                    return View(model);
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
