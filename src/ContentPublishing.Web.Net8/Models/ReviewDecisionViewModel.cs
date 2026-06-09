using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Net8.Models;

public sealed class ReviewDecisionViewModel
{
    [Required]
    public Guid ContentId { get; set; }

    [StringLength(2000)]
    public string? Comments { get; set; }
}