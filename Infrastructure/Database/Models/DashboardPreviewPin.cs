using System.ComponentModel.DataAnnotations;

namespace Application.Infrastructure.Database.Models;

public class DashboardPreviewPin
{
    [Key]
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Pin { get; set; } = null!;

    [Required]
    public Guid DashboardId { get; set; }

    [Required]
    public DateTime Expiration { get; set; }
}
