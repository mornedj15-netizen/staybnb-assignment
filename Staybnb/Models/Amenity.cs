using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

public class Amenity
{
    public int Id { get; set; }
    [StringLength(50)] public string Name { get; set; } = "";
    [StringLength(50)] public string IconClass { get; set; } = "";
    public ICollection<HostProperty> Properties { get; set; } = new List<HostProperty>();
}