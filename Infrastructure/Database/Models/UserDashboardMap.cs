using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Infrastructure.Database.Models;

[Table("UserDashboardMap")]
public class UserDashboardMap
{
    public Guid UserId { get; set; }
    public UserDo User { get; set; } = null!;

    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
}
