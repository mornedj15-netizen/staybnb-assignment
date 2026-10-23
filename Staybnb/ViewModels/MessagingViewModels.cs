using Staybnb.Models;

namespace Staybnb.ViewModels;

public class ConversationSummary
{
    public string OtherUserId { get; set; } = "";
    public string OtherName { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public DateTime LastTimestamp { get; set; }
    public int UnreadCount { get; set; }
}

public class NotificationBellViewModel
{
    public int UnreadNotifications { get; set; }
    public int UnreadMessages { get; set; }
    public List<Notification> Latest { get; set; } = new();
}