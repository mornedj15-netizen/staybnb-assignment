using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Data;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var env = services.GetRequiredService<IWebHostEnvironment>();

        // Amenities
        if (!await db.Amenities.AnyAsync())
        {
            db.Amenities.AddRange(
                new Amenity { Name = "Wifi", IconClass = "wifi" },
                new Amenity { Name = "Kitchen", IconClass = "kitchen" },
                new Amenity { Name = "Washer", IconClass = "washer" },
                new Amenity { Name = "Air conditioning", IconClass = "snowflake" },
                new Amenity { Name = "Pool", IconClass = "pool" },
                new Amenity { Name = "Free parking", IconClass = "parking" });
            await db.SaveChangesAsync();
        }

        // Demo accounts
        var host = await EnsureUserAsync(userManager, "host@staybnb.com", "Host@123", "Demo", "Host", "Host");
        await EnsureUserAsync(userManager, "guest@staybnb.com", "Guest@123", "Demo", "Guest", "Guest");

        if (host == null || await db.Properties.AnyAsync()) return;

        var amenities = await db.Amenities.ToListAsync();
        var samples = new[]
        {
            (Title: "Luxury City Apartment", City: "Durban", Type: "Apartment", Price: 1320m, Cleaning: 250m, Guests: 4, Image: "seed1.jpg",
             Description: "A bright apartment close to the beachfront, restaurants and shops."),
            (Title: "Cozy Forest Cabin", City: "Drakensberg", Type: "Cabin", Price: 3100m, Cleaning: 300m, Guests: 8, Image: "seed2.jpg",
             Description: "A quiet cabin among the trees with a deck, fireplace and mountain views."),
            (Title: "Beachfront Villa", City: "Umhlanga", Type: "Villa", Price: 2100m, Cleaning: 400m, Guests: 6, Image: "seed3.jpg",
             Description: "A spacious villa a few steps from the sea, with a pool and braai area.")
        };

        foreach (var s in samples)
        {
            // Skip a listing if its image is missing, because a listing must have an image
            if (!File.Exists(Path.Combine(env.WebRootPath, "images", "seed", s.Image))) continue;

            var property = new HostProperty
            {
                Title = s.Title,
                Description = s.Description,
                City = s.City,
                PropertyType = s.Type,
                Address = $"1 Sample Street, {s.City}",
                PricePerNight = s.Price,
                CleaningFee = s.Cleaning,
                MaxGuests = s.Guests,
                IsActive = true,
                HostId = host.Id,
                Amenities = amenities.Take(4).ToList(),
                CheckInProcess = new CheckInProcess
                {
                    Title = "Guest check-in",
                    StepsJson = JsonSerializer.Serialize(new CheckInSteps
                    {
                        Rules = new() { "Respect quiet hours after 22:00", "No smoking indoors" },
                        Documents = new() { "ID or Passport" }
                    })
                }
            };
            property.Images.Add(new PropertyImage { ImageUrl = $"/images/seed/{s.Image}" });

            db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = host.Id,
                Property = property,
                Status = ApplicationStatus.Approved
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(UserManager<ApplicationUser> userManager,
        string email, string password, string first, string last, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user != null) return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = first,
            LastName = last
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return null;

        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}