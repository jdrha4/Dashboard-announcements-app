using Application.Infrastructure.Database.Models;

namespace Application.Areas.Account.Models;

public class UserRoleViewModel
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string UserEmail { get; set; } = default!;
    public UserRole CurrentRole { get; set; }
    public bool IsSelf { get; set; }
    public string? ProfileImageBase64 { get; set; }
}
