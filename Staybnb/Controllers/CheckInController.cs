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

[Authorize(Roles = "Guest")]
public class CheckInController : Controller
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
    private const long MaxFileSize = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly IActivityLogger _activityLogger;
    private readonly INotificationService _notifications;

    public CheckInController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env, IActivityLogger activityLogger, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
        _activityLogger = activityLogger;
        _notifications = notifications;
    }

    private string GuestId => _userManager.GetUserId(User)!;

    private string DocumentFolder => Path.Combine(_env.ContentRootPath, "App_Data", "guest-documents");

    private Task<Booking?> LoadBookingAsync(int bookingId) =>
        _db.Bookings
            .Include(b => b.Property).ThenInclude(p => p.CheckInProcess)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == GuestId);

    [HttpGet]
    public async Task<IActionResult> Index(int bookingId, int step = 1)
    {
        var booking = await LoadBookingAsync(bookingId);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Approved && booking.Status != BookingStatus.CheckedIn)
        {
            TempData["Error"] = "Check-in is only available for approved bookings.";
            return RedirectToAction("MyBookings", "Booking");
        }
        if (booking.Property.CheckInProcess == null)
        {
            TempData["Error"] = "The host has not set up check-in for this property yet.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var checkIn = booking.GuestCheckIn;
        if (checkIn == null)
        {
            checkIn = new GuestCheckIn
            {
                BookingId = booking.Id,
                CheckInProcessId = booking.Property.CheckInProcess.Id,
                Status = CheckInStatus.InProgress
            };
            _db.GuestCheckIns.Add(checkIn);
            await _db.SaveChangesAsync();
            await _activityLogger.LogAsync(GuestId, $"Check-in started for booking #{booking.Id}", ActivityType.BookingUpdate);
        }

        var steps = ReadSteps(booking);
        return View(new CheckInViewModel
        {
            Booking = booking,
            CheckIn = checkIn,
            Rules = steps.Rules,
            RequiredDocuments = steps.Documents,
            Step = Math.Clamp(step, 1, 3)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcknowledgeRules(int bookingId)
    {
        var booking = await LoadBookingAsync(bookingId);
        if (booking == null) return NotFound();

        await _activityLogger.LogAsync(GuestId, $"Acknowledged check-in rules for booking #{booking.Id}", ActivityType.BookingUpdate);
        return RedirectToAction(nameof(Index), new { bookingId, step = 2 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(int bookingId, string documentType, IFormFile? file)
    {
        var booking = await LoadBookingAsync(bookingId);
        if (booking?.GuestCheckIn == null) return NotFound();

        var checkIn = booking.GuestCheckIn;
        var required = ReadSteps(booking).Documents;

        if (checkIn.Status == CheckInStatus.Submitted || checkIn.Status == CheckInStatus.Verified)
            return Fail(bookingId, "This check-in is already submitted.");
        if (!required.Contains(documentType))
            return Fail(bookingId, "That document type is not required.");
        if (file == null || file.Length == 0)
            return Fail(bookingId, "Please choose a file.");
        if (file.Length > MaxFileSize)
            return Fail(bookingId, "The file must be 5 MB or smaller.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Fail(bookingId, "Only JPG, PNG or PDF files are allowed.");

        // Replace any earlier upload of this type (unless it was already verified)
        var existing = checkIn.Documents.FirstOrDefault(d => d.DocumentType == documentType);
        if (existing != null)
        {
            if (existing.Status == DocumentStatus.Verified)
                return Fail(bookingId, "That document is already verified.");

            var oldPath = Path.Combine(DocumentFolder, existing.FileName);
            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            _db.GuestDocuments.Remove(existing);
        }

        Directory.CreateDirectory(DocumentFolder);
        var storedName = $"{Guid.NewGuid()}{ext}";
        using (var stream = new FileStream(Path.Combine(DocumentFolder, storedName), FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        checkIn.Documents.Add(new GuestDocument
        {
            DocumentType = documentType,
            FileName = storedName,
            Status = DocumentStatus.Pending
        });

        if (checkIn.Status == CheckInStatus.Rejected)
            checkIn.Status = CheckInStatus.InProgress;

        await _db.SaveChangesAsync();
        await _activityLogger.LogAsync(GuestId, $"Uploaded '{documentType}' for booking #{booking.Id}", ActivityType.BookingUpdate);

        TempData["Success"] = $"{documentType} uploaded.";
        return RedirectToAction(nameof(Index), new { bookingId, step = 2 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int bookingId)
    {
        var booking = await LoadBookingAsync(bookingId);
        if (booking?.GuestCheckIn == null) return NotFound();

        var checkIn = booking.GuestCheckIn;
        var required = ReadSteps(booking).Documents;

        if (required.Any(r => !checkIn.Documents.Any(d => d.DocumentType == r)))
            return Fail(bookingId, "Please upload every required document first.", 2);
        if (checkIn.Documents.Any(d => d.Status == DocumentStatus.Rejected))
            return Fail(bookingId, "Please replace the rejected documents first.", 2);

        checkIn.Status = CheckInStatus.Submitted;
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(GuestId, $"Check-in submitted for booking #{booking.Id}", ActivityType.BookingUpdate);
        await _notifications.NotifyAsync(booking.Property.HostId, "Check-in Submitted",
            $"A guest submitted check-in documents for '{booking.Property.Title}'. Please verify them.",
            NotificationType.General);

        TempData["Success"] = "Check-in submitted. The host will verify your documents.";
        return RedirectToAction("MyBookings", "Booking");
    }

    private IActionResult Fail(int bookingId, string message, int step = 2)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index), new { bookingId, step });
    }

    private static CheckInSteps ReadSteps(Booking booking) =>
        JsonSerializer.Deserialize<CheckInSteps>(booking.Property.CheckInProcess!.StepsJson) ?? new CheckInSteps();
}