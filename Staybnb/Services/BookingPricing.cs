namespace Staybnb.Services;

public static class BookingPricing
{
    public const decimal ServiceFeeRate = 0.12m;   // 12% of the nightly subtotal

    public static decimal ServiceFee(decimal pricePerNight, int nights)
        => Math.Round(pricePerNight * nights * ServiceFeeRate, 2);

    // TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
    public static decimal Total(decimal pricePerNight, int nights, decimal cleaningFee)
        => (pricePerNight * nights) + cleaningFee + ServiceFee(pricePerNight, nights);
}