using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ContentPublishing.Web.Net8.Models;

namespace ContentPublishing.Web.Net8.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var model = new HomeDashboardViewModel
        {
            IsAuthenticated = isAuthenticated,
            IsAdmin = isAuthenticated && (User.IsInRole("Admin") || User.IsInRole("Administrator")),
            IsReviewer = isAuthenticated && User.IsInRole("Reviewer"),
            IsAuthor = isAuthenticated && User.IsInRole("Author")
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
