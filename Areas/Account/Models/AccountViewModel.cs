namespace Application.Areas.Account.Models;

public record AccountViewModel
{
    public string Email { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ProfileImageBase64 { get; set; }
}
