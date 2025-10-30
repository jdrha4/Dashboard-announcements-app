using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Account.Models;

public class EditAccountViewModel
{
    [Required]
    [EmailAddress]
    [MaxLength(512)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    public string? ProfileImageBase64 { get; set; }
    public IFormFile? ProfileImage { get; set; }

    public bool RemoveProfileImage { get; set; }
}
