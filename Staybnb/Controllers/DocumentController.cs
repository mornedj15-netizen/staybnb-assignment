using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize(Roles = "Guest,Host")]
public class DocumentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public DocumentController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public async Task<IActionResult> Download(int id)
    {
        var doc = await _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        // Only the booking's guest and the property's host may open it
        var userId = _userManager.GetUserId(User);
        var booking = doc.GuestCheckIn.Booking;
        if (booking.GuestId != userId && booking.Property.HostId != userId) return Forbid();

        var path = Path.Combine(_env.ContentRootPath, "App_Data", "guest-documents", doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(doc.FileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg"
        };
        return PhysicalFile(path, contentType);
    }
}