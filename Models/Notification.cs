namespace labsupport.Models
{
    public class Notification
    {
        public long Id { get; set; }
        public int UserId { get; set; }        // Кому уведомление
        public long? TicketId { get; set; }    // Связанная заявка (может быть null)
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual User User { get; set; } = null!;
        public virtual Ticket? Ticket { get; set; }
    }
}