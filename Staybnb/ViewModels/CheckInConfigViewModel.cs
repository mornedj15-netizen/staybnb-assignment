using System.ComponentModel.DataAnnotations;

namespace Staybnb.ViewModels;

public class CheckInConfigViewModel
{
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = "";

    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [Display(Name = "House rules (one per line)")]
    public string Rules { get; set; } = "";

    [Required(ErrorMessage = "List at least one required document.")]
    [Display(Name = "Required documents (one per line)")]
    public string Documents { get; set; } = "";
}

// What gets stored as JSON in CheckInProcess.StepsJson
public class CheckInSteps
{
    public List<string> Rules { get; set; } = new();
    public List<string> Documents { get; set; } = new();
}