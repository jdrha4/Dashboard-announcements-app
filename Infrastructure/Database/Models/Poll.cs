using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Infrastructure.Database.Models
{
    public class Poll
    {
        [Key]
        [ForeignKey(nameof(Announcement))]
        public Guid AnnouncementId { get; set; }

        [Required]
        public bool IsMultichoice { get; set; }
        public Announcement Announcement { get; set; } = null!;

        public ICollection<PollChoice> PollChoices { get; set; } = new List<PollChoice>();
    }
}
