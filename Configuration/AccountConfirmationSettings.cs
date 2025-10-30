using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Configuration;

public class AccountConfirmationSettings
{
    [Required]
    public bool RequireConfirmation { get; set; } = default;

    [Required]
    public int TokenExpirySeconds { get; set; } = 3600 * 24;
}
