using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;

namespace Staybnb.Controllers;

public class PropertiesController : Controller
{
    private readonly ApplicationDbContext _db;

    public PropertiesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? city, string? type)
    {
        var active = _db.Properties.Where(p => p.IsActive);

        var query = active.Include(p => p.Images).Include(p => p.Reviews).AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City == city);
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.PropertyType == type);

        ViewBag.Cities = await active.Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync();
        ViewBag.Types = await active.Select(p => p.PropertyType).Distinct().OrderBy(t => t).ToListAsync();
        ViewBag.SelectedCity = city;
        ViewBag.SelectedType = type;

        return View(await query.OrderByDescending(p => p.Id).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var property = await _db.Properties
            .Include(p => p.Host)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (property == null) return NotFound();
        return View(property);
    }
}