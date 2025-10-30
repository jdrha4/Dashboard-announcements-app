using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Configuration;

public enum EmailMode
{
    Smtp,
    Log,
}

public class EmailSettings
{
    [Required]
    public string Sender { get; set; } = default!;

    [Required]
    public string SmtpServer { get; set; } = default!;

    public int Port { get; set; } = 587;

    [Required]
    public string Username { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    public string SenderName { get; set; } = "AnnounceIt";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EmailMode Mode { get; set; } = EmailMode.Smtp;
}
