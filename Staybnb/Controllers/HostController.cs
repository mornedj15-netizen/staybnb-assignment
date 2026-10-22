using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Host")]
public class HostController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogger _activityLogger;
    private readonly INotificationService _notifications;

    public HostController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IActivityLogger activityLogger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _activityLogger = activityLogger;
        _notifications = notifications;
    }

    private string HostId => _userManager.GetUserId(User)!;

    // ---------- Dashboard ----------
    public async Task<IActionResult> Dashboard()
    {
        var properties = await _db.Properties
            .Include(p => p.Reviews)
            .Where(p => p.HostId == HostId)
            .ToListAsync();

        var bookings = _db.Bookings.Where(b => b.Property.HostId == HostId);

        ViewBag.TotalProperties = properties.Count;
        ViewBag.ActiveProperties = properties.Count(p => p.IsActive);
        ViewBag.TotalBookings = await bookings.CountAsync();
        ViewBag.PendingBookings = await bookings.CountAsync(b => b.Status == BookingStatus.Pending);
        ViewBag.Earnings = await _db.Payments
            .Where(p => p.Booking.Property.HostId == HostId && p.Status == PaymentStatus.Paid)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var reviews = properties.SelectMany(p => p.Reviews).ToList();
        ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
        ViewBag.ReviewCount = reviews.Count;

        return View();
    }

    // ---------- Properties ----------
    public async Task<IActionResult> Properties()
    {
        var properties = await _db.Properties
            .Include(p => p.Images)
            .Include(p => p.Bookings)
            .Where(p => p.HostId == HostId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        return View(properties);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == HostId);
        if (property == null) return NotFound();

        // A listing can only go live once an admin has approved it
        var approved = await _db.HostApplications
            .AnyAsync(a => a.PropertyId == id && a.Status == ApplicationStatus.Approved);
        if (!approved)
        {
            TempData["Error"] = "This property has not been approved by an admin yet.";
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(HostId,
            $"Property '{property.Title}' {(property.IsActive ? "activated" : "deactivated")}", ActivityType.Property);

        TempData["Success"] = $"'{property.Title}' is now {(property.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Booking requests ----------
    public async Task<IActionResult> Bookings()
    {
        var bookings = await _db.Bookings
            .Include(b => b.Property)
            .Include(b => b.Guest)
            .Include(b => b.Payment)
            .Where(b => b.Property.HostId == HostId)
            .OrderBy(b => b.Status)
            .ThenByDescending(b => b.CheckInDate)
            .ToListAsync();
        return View(bookings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Property).ThenInclude(p => p.CheckInProcess)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == HostId);

        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending bookings can be approved.";
            return RedirectToAction(nameof(Bookings));
        }

        // Block approving dates that overlap an already approved booking
        var overlap = await _db.Bookings.AnyAsync(o =>
            o.Id != booking.Id &&
            o.PropertyId == booking.PropertyId &&
            (o.Status == BookingStatus.Approved || o.Status == BookingStatus.CheckedIn) &&
            o.CheckInDate < booking.CheckOutDate && o.CheckOutDate > booking.CheckInDate);
        if (overlap)
        {
            TempData["Error"] = "Those dates overlap another approved booking.";
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Approved;

        // Payment is simulated: it is marked Paid when the host approves
        if (booking.Payment == null)
            booking.Payment = new Payment { Amount = booking.TotalPrice };
        booking.Payment.Status = PaymentStatus.Paid;

        // Make sure the property has a check-in process so the guest can check in
        if (booking.Property.CheckInProcess == null)
            booking.Property.CheckInProcess = DefaultProcess(booking.PropertyId);

        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(HostId, $"Booking #{booking.Id} approved", ActivityType.BookingUpdate);
        await _notifications.NotifyAsync(booking.GuestId, "Booking Approved",
            $"Your booking for '{booking.Property.Title}' ({booking.CheckInDate:yyyy-MM-dd} to {booking.CheckOutDate:yyyy-MM-dd}) was approved.",
            NotificationType.BookingApproved);

        TempData["Success"] = "Booking approved.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == HostId);

        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending bookings can be rejected.";
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Rejected;
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(HostId, $"Booking #{booking.Id} rejected", ActivityType.BookingUpdate);
        await _notifications.NotifyAsync(booking.GuestId, "Booking Rejected",
            $"Your booking request for '{booking.Property.Title}' was not accepted.",
            NotificationType.BookingRejected);

        TempData["Success"] = "Booking rejected.";
        return RedirectToAction(nameof(Bookings));
    }

    // ---------- Check-in process setup ----------
    [HttpGet]
    public async Task<IActionResult> CheckInProcess(int propertyId)
    {
        var property = await _db.Properties.Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == HostId);
        if (property == null) return NotFound();

        var model = new CheckInConfigViewModel
        {
            PropertyId = property.Id,
            PropertyTitle = property.Title,
            Title = property.CheckInProcess?.Title ?? "Guest check-in"
        };

        if (property.CheckInProcess != null)
        {
            var steps = JsonSerializer.Deserialize<CheckInSteps>(property.CheckInProcess.StepsJson) ?? new CheckInSteps();
            model.Rules = string.Join("\n", steps.Rules);
            model.Documents = string.Join("\n", steps.Documents);
        }
        else
        {
            model.Documents = "ID or Passport";
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInProcess(CheckInConfigViewModel model)
    {
        var property = await _db.Properties.Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.HostId == HostId);
        if (property == null) return NotFound();

        var documents = Lines(model.Documents);
        if (documents.Count == 0)
            ModelState.AddModelError(nameof(model.Documents), "List at least one required document.");

        if (!ModelState.IsValid)
        {
            model.PropertyTitle = property.Title;
            return View(model);
        }

        var json = JsonSerializer.Serialize(new CheckInSteps { Rules = Lines(model.Rules), Documents = documents });

        if (property.CheckInProcess == null)
        {
            property.CheckInProcess = new CheckInProcess { Title = model.Title, StepsJson = json };
        }
        else
        {
            property.CheckInProcess.Title = model.Title;
            property.CheckInProcess.StepsJson = json;
        }
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(HostId, $"Check-in process configured for '{property.Title}'", ActivityType.Property);
        TempData["Success"] = "Check-in process saved.";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Guest check-in verification ----------
    public async Task<IActionResult> CheckIns()
    {
        var list = await _db.GuestCheckIns
            .Include(c => c.Booking).ThenInclude(b => b.Guest)
            .Include(c => c.Booking).ThenInclude(b => b.Property)
            .Include(c => c.Documents)
            .Where(c => c.Booking.Property.HostId == HostId && c.Status >= CheckInStatus.Submitted)
            .OrderBy(c => c.Status)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> CheckInDetails(int id)
    {
        var checkIn = await _db.GuestCheckIns
            .Include(c => c.Booking).ThenInclude(b => b.Guest)
            .Include(c => c.Booking).ThenInclude(b => b.Property)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id && c.Booking.Property.HostId == HostId);
        if (checkIn == null) return NotFound();
        return View(checkIn);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(int id)
    {
        var doc = await LoadDocumentAsync(id);
        if (doc == null) return NotFound();

        var checkIn = doc.GuestCheckIn;
        doc.Status = DocumentStatus.Verified;

        // All required documents verified -> guest is checked in
        var required = JsonSerializer.Deserialize<CheckInSteps>(checkIn.CheckInProcess.StepsJson)?.Documents ?? new();
        var allVerified = required.All(t =>
            checkIn.Documents.Any(d => d.DocumentType == t && d.Status == DocumentStatus.Verified));

        if (allVerified)
        {
            checkIn.Status = CheckInStatus.Verified;
            checkIn.Booking.Status = BookingStatus.CheckedIn;
        }
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(HostId, $"Verified '{doc.DocumentType}' for booking #{checkIn.BookingId}", ActivityType.BookingUpdate);
        if (allVerified)
        {
            await _activityLogger.LogAsync(HostId, $"Booking #{checkIn.BookingId} checked in", ActivityType.BookingUpdate);
            await _notifications.NotifyAsync(checkIn.Booking.GuestId, "Check-in Complete",
                $"Your documents for '{checkIn.Booking.Property.Title}' were verified. You are checked in.",
                NotificationType.General);
        }

        TempData["Success"] = allVerified ? "All documents verified. Guest is checked in." : "Document verified.";
        return RedirectToAction(nameof(CheckInDetails), new { id = checkIn.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDocument(int id)
    {
        var doc = await LoadDocumentAsync(id);
        if (doc == null) return NotFound();

        var checkIn = doc.GuestCheckIn;
        doc.Status = DocumentStatus.Rejected;
        checkIn.Status = CheckInStatus.Rejected;
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(HostId, $"Rejected '{doc.DocumentType}' for booking #{checkIn.BookingId}", ActivityType.BookingUpdate);
        await _notifications.NotifyAsync(checkIn.Booking.GuestId, "Document Rejected",
            $"Your '{doc.DocumentType}' for '{checkIn.Booking.Property.Title}' was rejected. Please upload a clearer copy.",
            NotificationType.General);

        TempData["Success"] = "Document rejected. The guest was asked to re-upload.";
        return RedirectToAction(nameof(CheckInDetails), new { id = checkIn.Id });
    }

    private Task<GuestDocument?> LoadDocumentAsync(int id) =>
        _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.CheckInProcess)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Documents)
            .FirstOrDefaultAsync(d => d.Id == id && d.GuestCheckIn.Booking.Property.HostId == HostId);

    private static List<string> Lines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private static CheckInProcess DefaultProcess(int propertyId) => new()
    {
        PropertyId = propertyId,
        Title = "Guest check-in",
        StepsJson = JsonSerializer.Serialize(new CheckInSteps
        {
            Rules = new() { "Respect quiet hours", "No smoking indoors" },
            Documents = new() { "ID or Passport" }
        })
    };
}