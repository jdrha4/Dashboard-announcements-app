using System.ComponentModel.DataAnnotations;

namespace Application.Areas.Announcements.Models
{
    public class AnnouncementWithConversationViewModel : AnnouncementViewModel
    {
        public IEnumerable<CommentViewModel> ConversationMessages { get; set; } = new List<CommentViewModel>();

        public string BackgroundColor { get; set; } = "bg-light";

        [MaxLength(500)]
        public string? Comment { get; set; }

        public string? AuthorId { get; set; }

        public string? CurrentUserId { get; set; }

        public bool IsUserDashboardOwner { get; set; }

        public bool IsMultichoice { get; set; }

        public List<PollChoiceResultViewModel> PollChoices { get; set; } = new();

        public bool UserHasVoted { get; set; }
    }
}
