using System;

namespace Application.Areas.Announcements.Models
{
    public class CommentViewModel
    {
        public string Author { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime PostedAt { get; set; }
    }
}
