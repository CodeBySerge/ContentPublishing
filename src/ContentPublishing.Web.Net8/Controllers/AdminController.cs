using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContentPublishing.Web.Net8.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ContentReadDbContext _db;

    public AdminController(ContentReadDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var model = new AdminDashboardViewModel
        {
            TotalContent = await _db.Contents.CountAsync(),
            DraftCount = await _db.Contents.CountAsync(c => c.Status == "Draft"),
            UnderReviewCount = await _db.Contents.CountAsync(c => c.Status == "UnderReview"),
            PublishedCount = await _db.Contents.CountAsync(c => c.Status == "Published")
        };

        return View(model);
    }
}