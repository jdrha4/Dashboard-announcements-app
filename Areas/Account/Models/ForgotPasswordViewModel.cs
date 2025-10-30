using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Account.Models;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
