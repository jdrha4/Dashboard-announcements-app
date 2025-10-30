using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Account.Models;

public record RegisterViewModel
{
    [Required]
    [EmailAddress]
    [MaxLength(512)]
    [Display(Name = "Email Address")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(256)]
    [Display(Name = "Name")]
    public string Name { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MaxLength(256)]
    [Display(Name = "Password")]
    public string Password { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MaxLength(256)]
    [Display(Name = "Password repeat")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string PasswordRepeat { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
