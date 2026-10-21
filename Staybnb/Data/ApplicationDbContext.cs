using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> Properties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Money precision
        b.Entity<HostProperty>().Property(p => p.PricePerNight).HasPrecision(18, 2);
        b.Entity<HostProperty>().Property(p => p.CleaningFee).HasPrecision(18, 2);
        b.Entity<Booking>().Property(x => x.TotalPrice).HasPrecision(18, 2);
        b.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);

        // One-to-one relationships
        b.Entity<Booking>().HasOne(x => x.Payment).WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId);
        b.Entity<Booking>().HasOne(x => x.GuestCheckIn).WithOne(g => g.Booking)
            .HasForeignKey<GuestCheckIn>(g => g.BookingId);
        b.Entity<HostProperty>().HasOne(p => p.CheckInProcess).WithOne(c => c.Property)
            .HasForeignKey<CheckInProcess>(c => c.PropertyId);

        // Avoid multiple cascade paths in SQL Server
        b.Entity<Booking>().HasOne(x => x.Guest).WithMany(u => u.Bookings)
            .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne(x => x.Reviewer).WithMany(u => u.Reviews)
            .HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Message>().HasOne(m => m.Sender).WithMany()
            .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Message>().HasOne(m => m.Receiver).WithMany()
            .HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WishlistItem>().HasOne(w => w.User).WithMany()
            .HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HostApplication>().HasOne(a => a.Property).WithMany()
            .HasForeignKey(a => a.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestCheckIn>().HasOne(g => g.CheckInProcess).WithMany()
            .HasForeignKey(g => g.CheckInProcessId).OnDelete(DeleteBehavior.Restrict);
    }
}