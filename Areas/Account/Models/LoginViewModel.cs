using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Account.Models;

public record LoginViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }

    public string Message { get; init; } = string.Empty;
}
