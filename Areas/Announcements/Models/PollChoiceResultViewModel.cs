namespace Application.Areas.Announcements.Models
{
    public class PollChoiceResultViewModel
    {
        public Guid Id { get; set; }
        public string ChoiceText { get; set; } = string.Empty;
        public int VoteCount { get; set; }
        public bool HasUserVoted { get; set; }
    }
}
