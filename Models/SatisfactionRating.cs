using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class SatisfactionRating
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public int UserId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
