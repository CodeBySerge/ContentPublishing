using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Net8.Models;

public sealed class PendingReviewItemViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public int ChapterCount { get; init; }
}

public sealed class ReviewContentViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ContentStatus { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public string? ExistingComments { get; init; }
    public string? AuthorChangeNotes { get; init; }
    public IReadOnlyList<ReviewChapterItemViewModel> Chapters { get; init; } = Array.Empty<ReviewChapterItemViewModel>();
}

public sealed class ReviewChapterItemViewModel
{
    public Guid ChapterId { get; init; }
    public int ChapterOrder { get; init; }
    public string ChapterTitle { get; init; } = string.Empty;
    public string ChapterBody { get; init; } = string.Empty;
}

public sealed class ReviewClarificationRequestViewModel
{
    [Required]
    public Guid ContentId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;
}

public sealed class ReviewNotificationListItemViewModel
{
    public Guid ContentId { get; init; }
    public string NotificationType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
}

public sealed class ReviewNotificationsViewModel
{
    public int TotalNotifications { get; init; }
    public IReadOnlyList<ReviewNotificationListItemViewModel> Items { get; init; } = Array.Empty<ReviewNotificationListItemViewModel>();
}

public sealed class ReviewHistoryItemViewModel
{
    public Guid ReviewId { get; init; }
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Comments { get; init; }
    public DateTime SubmittedDate { get; init; }
    public DateTime? ReviewDate { get; init; }
}