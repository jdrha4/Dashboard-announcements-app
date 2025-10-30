using System.ComponentModel.DataAnnotations;
using Application.Areas.Announcements.Models;

namespace Application.Areas.Dashboards.Models;

public class DashboardDetailsViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid AuthorId { get; set; }

    public Guid? CurrentUserId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string ProfileImageBase64 { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<AnnouncementViewModel> Announcements { get; set; } = new();

    public Guid DashboardToken { get; set; }

    /// <summary>
    /// 0=two days, 1=one week, 2=two weeks, 3=one month, 4=two months, 5=six months
    /// </summary>
    [Display(Name = "Expiry (days)")]
    [Range(0, 5)]
    public int ExpiryOption { get; set; }

    /// <summary>
    /// The maximum number of announcements this dashboard may have.
    /// </summary>
    [Range(10, 200, ErrorMessage = "Must be between 10 and 200")]
    public int MaxAnnouncements { get; set; }

    public DashboardDetailsViewModel()
    {
        // constructor - to display default vals, cause not all dashboards now have this attribute.
        MaxAnnouncements = 50;
        ExpiryOption = 2;
    }
}
