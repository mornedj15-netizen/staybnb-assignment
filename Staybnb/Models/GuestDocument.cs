using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

public class GuestDocument
{
    public int Id { get; set; }
    public int GuestCheckInId { get; set; }
    public GuestCheckIn GuestCheckIn { get; set; } = null!;
    [StringLength(100)] public string DocumentType { get; set; } = "";  // ID or Passport
    [StringLength(200)] public string FileName { get; set; } = "";
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
}