using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

public class CheckInProcess
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    [StringLength(200)] public string Title { get; set; } = "";
    public string StepsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}