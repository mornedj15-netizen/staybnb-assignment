using Staybnb.Models;

public class Message
{
    public int Id { get; set; }
    public string SenderId { get; set; } = "";
    public ApplicationUser Sender { get; set; } = null!;
    public string ReceiverId { get; set; } = "";
    public ApplicationUser Receiver { get; set; } = null!;
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}