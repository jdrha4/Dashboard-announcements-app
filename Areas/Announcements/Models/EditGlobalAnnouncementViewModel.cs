using System.ComponentModel.DataAnnotations;
using Application.Infrastructure.Database.Models;

namespace Application.Areas.Announcements.Models;

public class EditGlobalAnnouncementViewModel
{
    [Required]
    public Guid Id { get; set; }

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

    public EditGlobalAnnouncementViewModel()
    {
        ExpirationDate = DateTime.UtcNow.Date.AddMonths(1);
    }

    public bool IsImportant { get; set; }

    public Guid CurrentUserId { get; set; }

    [Display(Name = "Target Dashboards")]
    [MinLength(1, ErrorMessage = "You must select at least one dashboard.")]
    public List<Guid> SelectedDashboardIds { get; set; } = new();

    public List<CreateGlobalAnnouncementViewModel.DashboardSelection> Dashboards { get; set; } = new();

    public Guid DashboardAuthorId { get; set; }
    public string PrevUrl { get; set; } = "/";
}
