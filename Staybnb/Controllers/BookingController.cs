using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Guest")]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogger _activityLogger;
    private readonly INotificationService _notifications;

    public BookingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IActivityLogger activityLogger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _activityLogger = activityLogger;
        _notifications = notifications;
    }

    // Step 1: show the price breakdown for the chosen dates
    [HttpGet]
    public async Task<IActionResult> Review(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
    {
        var (property, error) = await ValidateAsync(propertyId, checkIn, checkOut, guests);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "Properties", new { id = propertyId });
        }
        return View(BuildSummary(property!, checkIn, checkOut, guests));
    }

    // Step 2: confirm and create the booking request
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
    {
        var (property, error) = await ValidateAsync(propertyId, checkIn, checkOut, guests);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "Properties", new { id = propertyId });
        }

        // Price is always recalculated on the server, never trusted from the form
        var price = PricingCalculator.Calculate(property!.PricePerNight, property.CleaningFee, checkIn, checkOut);
        var guestId = _userManager.GetUserId(User)!;

        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = guestId,
            CheckInDate = checkIn.Date,
            CheckOutDate = checkOut.Date,
            NumberOfGuests = guests,
            TotalPrice = price.Total,
            Status = BookingStatus.Pending,
            Payment = new Payment { Amount = price.Total, Status = PaymentStatus.Pending }
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(guestId, $"Booking #{booking.Id} requested", ActivityType.BookingUpdate);
        await _notifications.NotifyAsync(property.HostId, "New Booking Request",
            $"A guest requested '{property.Title}' from {checkIn:yyyy-MM-dd} to {checkOut:yyyy-MM-dd}.",
            NotificationType.General);

        TempData["Success"] = "Booking request sent. The host will approve or reject it.";
        return RedirectToAction(nameof(MyBookings));
    }

    public async Task<IActionResult> MyBookings()
    {
        var guestId = _userManager.GetUserId(User)!;
        var bookings = await _db.Bookings
            .Include(b => b.Property).ThenInclude(p => p.Images)
            .Include(b => b.Payment)
            .Where(b => b.GuestId == guestId)
            .OrderByDescending(b => b.CheckInDate)
            .ToListAsync();
        return View(bookings);
    }

    private async Task<(HostProperty?, string?)> ValidateAsync(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
    {
        var property = await _db.Properties.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);

        if (property == null) return (null, "That property is not available.");
        if (checkIn.Date < DateTime.Today) return (null, "Check-in cannot be in the past.");
        if (checkOut.Date <= checkIn.Date) return (null, "Check-out must be after check-in.");
        if (guests < 1 || guests > property.MaxGuests)
            return (null, $"This property allows 1 to {property.MaxGuests} guests.");

        var overlaps = await _db.Bookings.AnyAsync(b =>
            b.PropertyId == propertyId &&
            (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
            b.CheckInDate < checkOut.Date && b.CheckOutDate > checkIn.Date);
        if (overlaps) return (null, "Those dates are already booked.");

        return (property, null);
    }

    private static BookingSummaryViewModel BuildSummary(HostProperty property, DateTime checkIn, DateTime checkOut, int guests)
    {
        var p = PricingCalculator.Calculate(property.PricePerNight, property.CleaningFee, checkIn, checkOut);
        return new BookingSummaryViewModel
        {
            Property = property,
            CheckIn = checkIn.Date,
            CheckOut = checkOut.Date,
            Guests = guests,
            Nights = p.Nights,
            Subtotal = p.Subtotal,
            CleaningFee = p.CleaningFee,
            ServiceFee = p.ServiceFee,
            Total = p.Total
        };
    }
}