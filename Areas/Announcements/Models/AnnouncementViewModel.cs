using Application.Infrastructure.Database.Models;

namespace Application.Areas.Announcements.Models;

public class AnnouncementViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnnouncementCategory? Category { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ProfileImageBase64 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsImportant { get; set; }
    public bool HasPoll { get; set; }
}
