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
public class ApplicationController : Controller
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly IActivityLogger _activityLogger;

    public ApplicationController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env, IActivityLogger activityLogger)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
        _activityLogger = activityLogger;
    }

    // Shows the guest's current application status, or the form if none exists
    public async Task<IActionResult> Status()
    {
        var userId = _userManager.GetUserId(User)!;
        var applications = await _db.HostApplications
            .Include(a => a.Property)
            .Where(a => a.ApplicationUserId == userId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();
        return View(applications);
    }

    [HttpGet]
    public IActionResult Apply() => View(new HostApplicationViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(HostApplicationViewModel model)
    {
        // An application is incomplete without at least one image
        if (model.Images == null || model.Images.Count == 0 || model.Images.All(i => i.Length == 0))
            ModelState.AddModelError(nameof(model.Images), "Please upload at least one image.");

        foreach (var file in model.Images ?? new())
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                ModelState.AddModelError(nameof(model.Images), "Only JPG, PNG or WEBP images are allowed.");
        }

        if (!ModelState.IsValid) return View(model);

        var userId = _userManager.GetUserId(User)!;

        var property = new HostProperty
        {
            Title = model.Title,
            Description = model.Description,
            PricePerNight = model.PricePerNight,
            CleaningFee = model.CleaningFee,
            Address = model.Address,
            City = model.City,
            PropertyType = model.PropertyType,
            MaxGuests = model.MaxGuests,
            IsActive = false,          // goes live only after admin approval
            HostId = userId
        };

        var folder = Path.Combine(_env.WebRootPath, "uploads", "properties");
        foreach (var file in model.Images.Where(i => i.Length > 0))
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create);
            await file.CopyToAsync(stream);
            property.Images.Add(new PropertyImage { ImageUrl = $"/uploads/properties/{fileName}" });
        }

        var application = new HostApplication
        {
            ApplicationUserId = userId,
            Property = property,
            Status = ApplicationStatus.Pending
        };

        // Saves the property, images and application together in one SaveChanges
        _db.HostApplications.Add(application);
        await _db.SaveChangesAsync();

        await _activityLogger.LogAsync(userId, "Host application submitted", ActivityType.Application);

        TempData["Success"] = "Your application was submitted and is under review.";
        return RedirectToAction(nameof(Status));
    }
}