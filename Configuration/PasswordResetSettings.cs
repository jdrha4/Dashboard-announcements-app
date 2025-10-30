using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Configuration;

public class PasswordResetSettings
{
    [Required]
    public int TokenExpirySeconds { get; set; } = 60 * 30;

    [Required]
    public int MaxActiveTokens { get; set; } = 3;
}
