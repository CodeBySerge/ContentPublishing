using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Net8.Models;

public sealed class ReviewRecord
{
    [Key]
    public Guid ReviewId { get; set; }

    [Required]
    public Guid ContentId { get; set; }

    [Required]
    [StringLength(128)]
    public string ReviewerId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    public string? Comments { get; set; }

    [StringLength(2000)]
    public string? AuthorChangeNotes { get; set; }

    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewDate { get; set; }
}