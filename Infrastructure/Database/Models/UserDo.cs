namespace Application.Infrastructure.Database.Models;

public enum UserRole
{
    User = 0,
    Admin = 1,
    Manager = 2,
}

public class UserDo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Email { get; set; }

    public required string Name { get; set; }

    public required byte[] PasswordHash { get; set; }

    public required byte[] PasswordSalt { get; set; }

    public bool Confirmed { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public string? ProfileImageBase64 { get; set; }

    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
    public ICollection<UserDashboardMap> UserDashboards { get; set; } = new List<UserDashboardMap>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
