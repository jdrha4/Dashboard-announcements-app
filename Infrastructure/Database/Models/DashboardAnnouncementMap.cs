using System.ComponentModel.DataAnnotations;

namespace Application.Infrastructure.Database.Models;

public class DashboardAnnouncementMap
{
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;

    public Guid AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = null!;
}
