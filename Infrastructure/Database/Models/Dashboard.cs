using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Database.Models;

public enum ExpiryOption : int
{
    [Display(Name = "1 week")]
    OneWeek = 7,

    [Display(Name = "2 weeks")]
    TwoWeeks = 14,

    [Display(Name = "1 month")]
    OneMonth = 30,

    [Display(Name = "2 months")]
    TwoMonths = 60,

    [Display(Name = "6 months")]
    SixMonths = 180,

    [Display(Name = "12 months")]
    TwelveMonths = 365,
}

[Index(nameof(Name), IsUnique = true)]
public class Dashboard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid AuthorId { get; set; }
    public UserDo Author { get; set; } = null!;

    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
    public ICollection<UserDashboardMap> UserDashboards { get; set; } = new List<UserDashboardMap>();

    [Required]
    public Guid DashboardToken { get; set; } = Guid.NewGuid();
    public ICollection<DashboardAnnouncementMap> DashboardAnnouncements { get; set; } =
        new List<DashboardAnnouncementMap>();

    /// <summary>
    /// Maximum number of announcements allowed on this dashboard. Default 50
    /// </summary>
    [Range(10, 200)]
    public int MaxAnnouncements { get; set; } = 50;

    /// <summary>
    /// The latest date an announcement may expire on this dashboard. 0 - 5
    /// </summary>
    [Required]
    public ExpiryOption ExpiryOption { get; set; } = ExpiryOption.TwoWeeks;
}
