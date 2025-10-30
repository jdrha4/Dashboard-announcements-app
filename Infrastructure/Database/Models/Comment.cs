using System.ComponentModel.DataAnnotations;

namespace Application.Infrastructure.Database.Models
{
    public class Comment
    {
        public Guid Id { get; set; }
        public Guid AnnouncementId { get; set; }
        public Guid UserId { get; set; }

        [MaxLength(500)]
        public string Content { get; set; } = string.Empty;
        public DateTime PostedAt { get; set; } = DateTime.UtcNow;

        public Announcement Announcement { get; set; } = null!;
        public UserDo User { get; set; } = null!;
    }
}
