using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Infrastructure.Database.Models;

public class EmailConfirmationToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public DateTime Expiration { get; set; }

    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    public UserDo User { get; set; } = null!;
}
