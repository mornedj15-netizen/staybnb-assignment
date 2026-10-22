using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task NotifyAsync(string userId, string title, string message, NotificationType type)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title.Length > 100 ? title[..100] : title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}