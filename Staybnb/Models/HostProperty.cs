using System.ComponentModel.DataAnnotations;
namespace Staybnb.Models;

public class HostProperty
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Title { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    public decimal PricePerNight { get; set; }
    public decimal CleaningFee { get; set; }
    [Required] public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string PropertyType { get; set; } = "";   // Apartment, Villa, Cabin...
    public int MaxGuests { get; set; } = 1;
    public bool IsActive { get; set; } = false;      // becomes true on approval

    public string HostId { get; set; } = "";
    public ApplicationUser Host { get; set; } = null!;

    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
    public CheckInProcess? CheckInProcess { get; set; }
}