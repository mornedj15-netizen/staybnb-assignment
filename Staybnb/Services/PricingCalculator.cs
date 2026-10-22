namespace Staybnb.Services;

public static class PricingCalculator
{
    // Service fee is 12% of the nightly subtotal
    public const decimal ServiceFeeRate = 0.12m;

    public static (int Nights, decimal Subtotal, decimal CleaningFee, decimal ServiceFee, decimal Total)
        Calculate(decimal pricePerNight, decimal cleaningFee, DateTime checkIn, DateTime checkOut)
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        var subtotal = pricePerNight * nights;
        var serviceFee = Math.Round(subtotal * ServiceFeeRate, 2);
        var total = subtotal + cleaningFee + serviceFee;
        return (nights, subtotal, cleaningFee, serviceFee, total);
    }
}