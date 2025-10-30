namespace Application.Areas.Dashboards.Models;

public class AssignUsersViewModel
{
    public Guid DashboardId { get; set; }
    public string DashboardName { get; set; } = string.Empty;
    public List<UserSelection> Users { get; set; } = new();

    public class UserSelection
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }
}
