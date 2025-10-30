using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Dashboards.Models;

public class DashboardViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public int AnnouncementCount { get; set; }

    public string ProfileImageBase64 { get; set; } = string.Empty;

    [Range(0, 5)]
    public int ExpiryOption { get; set; }

    [Range(10, 200)]
    public int MaxAnnouncements { get; set; }
}
