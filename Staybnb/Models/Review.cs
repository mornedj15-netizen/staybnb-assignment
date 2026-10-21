using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

public class Review
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    public string ReviewerId { get; set; } = "";
    public ApplicationUser Reviewer { get; set; } = null!;
    [Range(1, 5)] public int Rating { get; set; }
    [StringLength(1000)] public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}