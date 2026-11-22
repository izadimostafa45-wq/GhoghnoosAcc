using System.ComponentModel.DataAnnotations;
namespace GhoghnoosAcc.Models;
public class JournalEntry
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string DocumentNumber { get; set; } = "";
    public DateTime DocumentDate { get; set; } = DateTime.Today;
    [StringLength(500)] public string? Description { get; set; }
    public bool IsPosted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<JournalEntryLine> Lines { get; set; } = [];
}
