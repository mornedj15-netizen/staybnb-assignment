using Staybnb.Models;

namespace Staybnb.ViewModels;

public class BookingSummaryViewModel
{
    public HostProperty Property { get; set; } = null!;
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }
    public int Nights { get; set; }
    public decimal Subtotal { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
}