using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifications;

    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
    }

    private string Me => _userManager.GetUserId(User)!;

    // Inbox: one row per person you have talked to
    public async Task<IActionResult> Inbox()
    {
        var me = Me;
        var messages = await _db.Messages
            .Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderId == me || m.ReceiverId == me)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var conversations = messages
            .GroupBy(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
            .Select(g =>
            {
                var last = g.First();
                var other = last.SenderId == me ? last.Receiver : last.Sender;
                return new ConversationSummary
                {
                    OtherUserId = g.Key,
                    OtherName = $"{other.FirstName} {other.LastName}",
                    LastMessage = last.Content,
                    LastTimestamp = last.Timestamp,
                    UnreadCount = g.Count(m => m.ReceiverId == me && !m.IsRead)
                };
            })
            .ToList();

        return View(conversations);
    }

    // A thread with one person. Opening it marks their messages to you as read.
    [HttpGet]
    public async Task<IActionResult> Conversation(string userId)
    {
        var me = Me;
        if (userId == me) return RedirectToAction(nameof(Inbox));

        var other = await _userManager.FindByIdAsync(userId);
        if (other == null) return NotFound();

        var messages = await _db.Messages
            .Where(m => (m.SenderId == me && m.ReceiverId == userId) ||
                        (m.SenderId == userId && m.ReceiverId == me))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        var unread = messages.Where(m => m.ReceiverId == me && !m.IsRead).ToList();
        if (unread.Any())
        {
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();
        }

        ViewBag.OtherId = other.Id;
        ViewBag.OtherName = $"{other.FirstName} {other.LastName}";
        ViewBag.Me = me;
        return View(messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string receiverId, string content)
    {
        var me = Me;
        content = (content ?? "").Trim();

        if (receiverId == me) return RedirectToAction(nameof(Inbox));
        if (content.Length == 0 || content.Length > 2000)
        {
            TempData["Error"] = "A message must be between 1 and 2000 characters.";
            return RedirectToAction(nameof(Conversation), new { userId = receiverId });
        }

        var receiver = await _userManager.FindByIdAsync(receiverId);
        if (receiver == null) return NotFound();

        _db.Messages.Add(new Message
        {
            SenderId = me,
            ReceiverId = receiverId,
            Content = content,
            Timestamp = DateTime.UtcNow,
            IsRead = false
        });
        await _db.SaveChangesAsync();

        var sender = await _userManager.FindByIdAsync(me);
        await _notifications.NotifyAsync(receiverId, "New Message",
            $"You have a new message from {sender!.FirstName} {sender.LastName}.",
            NotificationType.NewMessage);

        return RedirectToAction(nameof(Conversation), new { userId = receiverId });
    }
}