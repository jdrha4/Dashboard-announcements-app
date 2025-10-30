using System.ComponentModel.DataAnnotations;
using Application.Infrastructure.Database.Models;

namespace Application.Areas.Announcements.Models;

public class CreateGlobalAnnouncementViewModel
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public AnnouncementCategory? Category { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; }

    public CreateGlobalAnnouncementViewModel()
    {
        ExpirationDate = DateTime.UtcNow.Date.AddMonths(1);
    }

    public bool IsImportant { get; set; }

    // The current logged-in user
    public Guid CurrentUserId { get; set; }

    // List of dashboard IDs the announcement should appear on
    [Display(Name = "Target Dashboards")]
    [MinLength(1, ErrorMessage = "You must select at least one dashboard.")]
    public List<Guid> SelectedDashboardIds { get; set; } = new();

    // All available dashboards for selection in the form
    public List<DashboardSelection> Dashboards { get; set; } = new();

    public Guid DashboardAuthorId { get; set; }

    public class DashboardSelection
    {
        public Guid DashboardId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
