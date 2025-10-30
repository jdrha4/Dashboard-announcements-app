using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Infrastructure.Database.Models
{
    public class PollChoice
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ForeignKey(nameof(Poll))]
        public Guid PollId { get; set; }

        [Required]
        public string ChoiceText { get; set; } = string.Empty;

        public Poll Poll { get; set; } = null!;

        public ICollection<PollVote> PollVotes { get; set; } = new List<PollVote>();
    }
}
