using System.ComponentModel.DataAnnotations;
using Application.Infrastructure.Database.Models;

namespace Application.Areas.Announcements.Models;

public class EditAnnouncementViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100, ErrorMessage = "Title cannot be longer than 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500, ErrorMessage = "Description cannot be longer than 500 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public AnnouncementCategory? Category { get; set; }

    [Required]
    public Guid DashboardId { get; set; }

    [Required]
    public string PrevUrl { get; set; } = string.Empty;

    public bool IsImportant { get; set; }

    public Guid? CurrentUserId { get; set; }

    public Guid DashboardAuthorId { get; set; }
}
