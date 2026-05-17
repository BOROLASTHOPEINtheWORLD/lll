namespace labsupport.ViewModels
{
    public class NotificationDto
    {
        public long Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public long? TicketId { get; set; }
        public string? TicketNumber { get; set; }  // новое поле
        public string CreatedAt { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }
}
