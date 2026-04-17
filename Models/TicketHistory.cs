using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class TicketHistory
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public int UserId { get; set; }

    public string FieldName { get; set; } = null!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime? ChangedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
