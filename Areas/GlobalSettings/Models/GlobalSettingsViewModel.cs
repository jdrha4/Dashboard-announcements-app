using System.ComponentModel.DataAnnotations;

namespace Application.Areas.GlobalSettings.Models;

public class GlobalSettingsViewModel
{
    [Required]
    [RegularExpression(
        @"^(?!\-)(?:[a-zA-Z0-9-]{1,63}\.)+[a-zA-Z]{2,}$",
        ErrorMessage = "Please enter a valid domain name"
    )]
    [Display(Name = "Domain")]
    public string NewDomain { get; set; } = string.Empty;
    public List<string> AllowedDomainList { get; set; } = new();
}
