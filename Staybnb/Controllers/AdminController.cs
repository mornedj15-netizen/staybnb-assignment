using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogger _activityLogger;
    private readonly INotificationService _notifications;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IActivityLogger activityLogger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _activityLogger = activityLogger;
        _notifications = notifications;
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.PendingApps = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending);
        ViewBag.ActiveProperties = await _db.Properties.CountAsync(p => p.IsActive);
        ViewBag.TotalBookings = await _db.Bookings.CountAsync();
        return View();
    }

    public async Task<IActionResult> HostRequests()
    {
        var applications = await _db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property).ThenInclude(p => p.Images)
            .OrderBy(a => a.Status)
            .ThenByDescending(a => a.AppliedAt)
            .ToListAsync();
        return View(applications);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var application = await _db.HostApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null || application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "That application is no longer pending.";
            return RedirectToAction(nameof(HostRequests));
        }

        var user = await _userManager.FindByIdAsync(application.ApplicationUserId);
        if (user == null) return NotFound();

        // The transition: add Host, remove Guest
        if (!await _userManager.IsInRoleAsync(user, "Host"))
        {
            var added = await _userManager.AddToRoleAsync(user, "Host");
            if (!added.Succeeded)
            {
                TempData["Error"] = "Could not add the Host role.";
                return RedirectToAction(nameof(HostRequests));
            }
        }
        if (await _userManager.IsInRoleAsync(user, "Guest"))
        {
            await _userManager.RemoveFromRoleAsync(user, "Guest");
        }
        await _userManager.UpdateSecurityStampAsync(user);

        application.Status = ApplicationStatus.Approved;
        application.Property.IsActive = true;
        await _db.SaveChangesAsync();

        var adminId = _userManager.GetUserId(User);
        await _activityLogger.LogAsync(adminId, $"Approved host application #{application.Id}", ActivityType.Application);
        await _activityLogger.LogAsync(user.Id, "Role changed: Guest to Host", ActivityType.RoleChange);
        await _notifications.NotifyAsync(user.Id, "Host Application Approved",
            $"Your application for '{application.Property.Title}' has been approved. Log in again to see your Host dashboard.",
            NotificationType.ApplicationUpdate);

        TempData["Success"] = $"Approved. {user.FirstName} {user.LastName} is now a Host.";
        return RedirectToAction(nameof(HostRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var application = await _db.HostApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null || application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "That application is no longer pending.";
            return RedirectToAction(nameof(HostRequests));
        }

        application.Status = ApplicationStatus.Rejected;
        application.Property.IsActive = false;
        await _db.SaveChangesAsync();

        var adminId = _userManager.GetUserId(User);
        await _activityLogger.LogAsync(adminId, $"Rejected host application #{application.Id}", ActivityType.Application);
        await _notifications.NotifyAsync(application.ApplicationUserId, "Host Application Rejected",
            $"Your application for '{application.Property.Title}' was not approved.",
            NotificationType.ApplicationUpdate);

        TempData["Success"] = "Application rejected.";
        return RedirectToAction(nameof(HostRequests));
    }

    // ---------- User management (SuperAdmin only) ----------

    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Users(string? search)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Email!.Contains(search)
                                  || u.FirstName.Contains(search)
                                  || u.LastName.Contains(search));
        }

        var users = await query.OrderBy(u => u.Email).ToListAsync();

        var rows = new List<UserRowViewModel>();
        foreach (var u in users)
        {
            rows.Add(new UserRowViewModel
            {
                Id = u.Id,
                Name = $"{u.FirstName} {u.LastName}",
                Email = u.Email ?? "",
                Roles = (await _userManager.GetRolesAsync(u)).ToList()
            });
        }

        ViewBag.Search = search;
        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> PromoteToAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Only Guests can be promoted
        if (!await _userManager.IsInRoleAsync(user, "Guest"))
        {
            TempData["Error"] = "Only users with the Guest role can be promoted to Admin.";
            return RedirectToAction(nameof(Users));
        }

        var added = await _userManager.AddToRoleAsync(user, "Admin");
        if (!added.Succeeded)
        {
            TempData["Error"] = "Could not add the Admin role.";
            return RedirectToAction(nameof(Users));
        }

        await _userManager.RemoveFromRoleAsync(user, "Guest");
        await _userManager.UpdateSecurityStampAsync(user);

        var actorId = _userManager.GetUserId(User);
        await _activityLogger.LogAsync(actorId, $"Promoted {user.Email} to Admin", ActivityType.RoleChange);
        await _notifications.NotifyAsync(user.Id, "You are now an Admin",
            "A SuperAdmin has promoted your account to Admin. Log in again to see the Admin Dashboard.",
            NotificationType.General);

        TempData["Success"] = $"{user.Email} is now an Admin.";
        return RedirectToAction(nameof(Users));
    }

    // ---------- Activity log ----------

    public async Task<IActionResult> ActivityLogs(ActivityType? type)
    {
        var query = _db.ActivityLogs.Include(l => l.User).AsQueryable();

        if (type.HasValue)
            query = query.Where(l => l.ActivityType == type.Value);

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.SelectedType = type;
        return View(logs);
    }
}