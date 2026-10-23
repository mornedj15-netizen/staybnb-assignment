using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string Me => _userManager.GetUserId(User)!;

    public async Task<IActionResult> Index(string filter = "all", int? selectedId = null)
    {
        var me = Me;

        // Opening a notification marks it as read
        if (selectedId.HasValue)
        {
            var selected = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == selectedId && n.UserId == me);
            if (selected is { IsRead: false })
            {
                selected.IsRead = true;
                await _db.SaveChangesAsync();
            }
        }

        var all = _db.Notifications.Where(n => n.UserId == me);
        ViewBag.AllCount = await all.CountAsync();
        ViewBag.UnreadCount = await all.CountAsync(n => !n.IsRead);
        ViewBag.ReadCount = await all.CountAsync(n => n.IsRead);

        var query = filter switch
        {
            "unread" => all.Where(n => !n.IsRead),
            "read" => all.Where(n => n.IsRead),
            _ => all
        };

        ViewBag.Filter = filter;
        ViewBag.SelectedId = selectedId;
        return View(await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var me = Me;
        var unread = await _db.Notifications.Where(n => n.UserId == me && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var me = Me;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me);
        if (n != null)
        {
            _db.Notifications.Remove(n);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}