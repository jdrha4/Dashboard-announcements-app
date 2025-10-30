using System.ComponentModel.DataAnnotations;
using Application.Infrastructure.Database.Models;

namespace Application.Areas.Announcements.Models;

public class CreateAnnouncementViewModel
{
    [Required]
    [MaxLength(100, ErrorMessage = "Title cannot be longer than 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public AnnouncementCategory? Category { get; set; }

    public bool IsImportant { get; set; }

    public Guid? CurrentUserId { get; set; }

    public Guid DashboardAuthorId { get; set; }

    [DataType(DataType.Date)]
    public DateTime MaxAllowedExpirationDate { get; set; }

    [DataType(DataType.Date)]
    [Required]
    public DateTime ExpirationDate { get; set; }

    public bool HasPoll { get; set; }

    public bool IsMultichoice { get; set; }

    public List<string> PollChoices { get; set; } = new();
}
