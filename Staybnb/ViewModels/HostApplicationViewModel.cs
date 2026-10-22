using System.ComponentModel.DataAnnotations;

namespace Staybnb.ViewModels;

public class HostApplicationViewModel
{
    [Required, StringLength(100)]
    public string Title { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    [Required, Range(1, 1000000)]
    [Display(Name = "Price per night")]
    public decimal PricePerNight { get; set; }

    [Range(0, 1000000)]
    [Display(Name = "Cleaning fee")]
    public decimal CleaningFee { get; set; }

    [Required]
    public string Address { get; set; } = "";

    [Required]
    public string City { get; set; } = "";

    [Required]
    [Display(Name = "Property type")]
    public string PropertyType { get; set; } = "";

    [Required, Range(1, 50)]
    [Display(Name = "Max guests")]
    public int MaxGuests { get; set; } = 1;

    [Required(ErrorMessage = "Please upload at least one image.")]
    public List<IFormFile> Images { get; set; } = new();
}