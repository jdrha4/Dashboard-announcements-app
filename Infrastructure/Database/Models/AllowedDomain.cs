using System.ComponentModel.DataAnnotations;

namespace Application.Infrastructure.Database.Models;

public class AllowedDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Domain { get; set; } = string.Empty;

    public Guid GlobalSettingsId { get; set; }
    public GlobalSettings GlobalSettings { get; set; } = null!;
}
