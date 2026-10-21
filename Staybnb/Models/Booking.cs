using Staybnb.Models;

public class Booking
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    public string GuestId { get; set; } = "";
    public ApplicationUser Guest { get; set; } = null!;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public Payment? Payment { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }
}