namespace labsupport.Models
{
    public class TicketPriorityChange
    {
        public int Id { get; set; }
        public long TicketId { get; set; }
        public int ChangedById { get; set; }
        public short OldPriority { get; set; }
        public short NewPriority { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }

        // Навигационные свойства
        public virtual Ticket Ticket { get; set; } = null!;
        public virtual User ChangedBy { get; set; } = null!;
    }
}
