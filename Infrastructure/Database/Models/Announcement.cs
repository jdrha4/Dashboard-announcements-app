using System.ComponentModel.DataAnnotations;

namespace Application.Infrastructure.Database.Models
{
    public class Announcement
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public AnnouncementCategory? Category { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid UserId { get; set; }
        public UserDo User { get; set; } = null!;

        public Guid? DashboardId { get; set; }
        public Dashboard? Dashboard { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();

        [Required]
        public DateTime ExpirationDate { get; set; }

        public bool IsImportant { get; set; }

        public bool IsPoll { get; set; }

        public Poll? Poll { get; set; }

        public ICollection<DashboardAnnouncementMap> DashboardAnnouncements { get; set; } =
            new List<DashboardAnnouncementMap>();
    }

    public enum AnnouncementCategory
    {
        Borrowing,
        RenovationNotice,
        LostAndFound,
        Technical,
        BuySell,
        General,
        Unsorted,
    }
}
