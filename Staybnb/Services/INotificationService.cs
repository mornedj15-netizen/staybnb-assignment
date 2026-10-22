using Staybnb.Models;

namespace Staybnb.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string message, NotificationType type);
}