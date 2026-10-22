using Staybnb.Models;

namespace Staybnb.ViewModels;

public class CheckInViewModel
{
    public Booking Booking { get; set; } = null!;
    public GuestCheckIn CheckIn { get; set; } = null!;
    public List<string> Rules { get; set; } = new();
    public List<string> RequiredDocuments { get; set; } = new();
    public int Step { get; set; } = 1;
}