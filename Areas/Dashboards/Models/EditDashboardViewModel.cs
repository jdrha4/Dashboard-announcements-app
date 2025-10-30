using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Dashboards.Models
{
    public class EditDashboardViewModel
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }
    }
}
