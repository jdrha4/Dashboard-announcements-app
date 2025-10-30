namespace Application.Infrastructure.Database.Models;

public class GlobalSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public List<AllowedDomain> AllowedEmailDomains { get; set; } = new();

    public string? AllowedEmailRegex { get; set; }
}
