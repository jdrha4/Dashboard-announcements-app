using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Infrastructure.Database.Models
{
    public class PollVote
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }

        [Required]
        [ForeignKey(nameof(PollChoice))]
        public Guid PollChoiceId { get; set; }

        public UserDo User { get; set; } = null!;

        public PollChoice PollChoice { get; set; } = null!;
    }
}
