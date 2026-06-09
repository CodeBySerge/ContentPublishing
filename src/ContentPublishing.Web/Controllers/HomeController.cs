using ContentPublishing.Web.Models;
using Microsoft.AspNet.Identity;
using System;
using System.Linq;
using System.Web.Mvc;

namespace ContentPublishing.Web.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public JsonResult RoleState()
        {
            try
            {
                var isAuthenticated = Request.IsAuthenticated;
                var isAdmin = false;
                var isReviewer = false;
                var isAuthor = false;

                if (isAuthenticated)
                {
                    isAdmin = User.IsInRole("Admin") || User.IsInRole("Administrator");
                    isReviewer = User.IsInRole("Reviewer");
                    isAuthor = User.IsInRole("Author");

                    var userId = User.Identity.GetUserId();
                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        using (var db = ApplicationDbContext.Create())
                        {
                            var roleNames = db.Users
                                .Where(u => u.Id == userId)
                                .SelectMany(u => u.Roles)
                                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                                .ToList();

                            var roleById = db.Users
                                .Where(u => u.Id == userId)
                                .Select(u => db.Roles.Where(r => r.Id == u.RoleId).Select(r => r.Name).FirstOrDefault())
                                .FirstOrDefault();

                            isAdmin = isAdmin || roleNames.Contains("Administrator") || string.Equals(roleById, "Administrator", StringComparison.OrdinalIgnoreCase);
                            isReviewer = isReviewer || roleNames.Contains("Reviewer") || string.Equals(roleById, "Reviewer", StringComparison.OrdinalIgnoreCase);
                            isAuthor = isAuthor || roleNames.Contains("Author") || string.Equals(roleById, "Author", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }

                var payload = new
                {
                    isAuthenticated,
                    isAdmin,
                    isReviewer,
                    isAuthor
                };

                return Json(new ResponseWithValue("1", "Success", payload), JsonRequestBehavior.AllowGet);
            }
            catch (Exception)
            {
                return Json(new ResponseWithValue("0", "Failed to resolve role state", null), JsonRequestBehavior.AllowGet);
            }
        }
    }
}
