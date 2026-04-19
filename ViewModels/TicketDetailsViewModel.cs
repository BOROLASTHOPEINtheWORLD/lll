using labsupport.Models;

namespace labsupport.ViewModels
{
    public class TicketDetailsViewModel
    {
        public Ticket Ticket { get; set; } = null!;
        public List<TicketComment> Comments { get; set; } = new();
        public List<TicketHistory> History { get; set; } = new();
        public IEnumerable<MainCategory> Categories { get; set; } = new List<MainCategory>();
        public IEnumerable<User> AvailableAssignees { get; set; } = new List<User>();
        public User CurrentUser { get; set; } = null!;
    }
}
