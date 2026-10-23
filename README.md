# Staybnb - Airbnb Clone (Programming 742)

ASP.NET Core MVC, EF Core, SQL Server (LocalDB).

## How to run
1. Open `Staybnb.sln` in Visual Studio 2026 (.NET 10 SDK).
2. Press F5. The database is created and seeded automatically on first run.
   Connection string: `appsettings.json` (SQL Server LocalDB).
3. Optional: `staybnb-schema.sql` contains the full schema script.

## Test accounts
| Role | Email | Password |
|---|---|---|
| SuperAdmin | superadmin@staybnb.com | SuperAdmin@123 |
| Host | host@staybnb.com | Host@123 |
| Guest | guest@staybnb.com | Guest@123 |

## Notes
- New registrations are Guests. Approval of a host application swaps Guest for Host.
- Total price = (price per night x nights) + cleaning fee + service fee (12% of the nightly subtotal).
- Payments are simulated: marked Paid when the host approves a booking.
- Guest documents are stored in `App_Data/guest-documents` and served only to the guest and host.

