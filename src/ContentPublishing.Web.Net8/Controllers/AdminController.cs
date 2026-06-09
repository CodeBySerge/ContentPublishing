using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
            ApprovedCount = await _db.Contents.CountAsync(c => c.Status == "Approved"),
            PublishedCount = await _db.Contents.CountAsync(c => c.Status == "Published")
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ContentManagement()
    {
        var readyToPublish = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "Approved")
            .OrderByDescending(c => c.LastModifiedDate)
            .Select(c => new AdminContentItemViewModel
            {
                ContentId = c.ContentId,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                PublishedDate = c.PublishedDate
            })
            .ToListAsync();

        var recentPublished = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "Published")
            .OrderByDescending(c => c.PublishedDate)
            .Take(20)
            .Select(c => new AdminContentItemViewModel
            {
                ContentId = c.ContentId,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                PublishedDate = c.PublishedDate
            })
            .ToListAsync();

        return View(new AdminContentManagementViewModel
        {
            ReadyToPublish = readyToPublish,
            RecentPublished = recentPublished
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid contentId)
    {
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == contentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(ContentManagement));
        }

        if (!string.Equals(content.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only Approved content can be published.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var previousStatus = content.Status;
        content.Status = "Published";
        content.PublishedDate = DateTime.UtcNow;
        content.LastModifiedDate = DateTime.UtcNow;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = userId,
            Action = AuditActions.Publish,
            EntityType = "Content",
            EntityId = contentId,
            OldValue = previousStatus,
            NewValue = "Published",
            IpAddress = ip,
            ChangeDetails = "Admin published approved content."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content published successfully.";
        return RedirectToAction(nameof(ContentManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToDraft(Guid contentId)
    {
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == contentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var previousStatus = content.Status;
        content.Status = "Draft";
        content.PublishedDate = null;
        content.LastModifiedDate = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = AuditActions.StatusChange,
            EntityType = "Content",
            EntityId = contentId,
            OldValue = previousStatus,
            NewValue = "Draft",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ChangeDetails = "Admin moved content back to draft."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content moved back to Draft.";
        return RedirectToAction(nameof(ContentManagement));
    }
}