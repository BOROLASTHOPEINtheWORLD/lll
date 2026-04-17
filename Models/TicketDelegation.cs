using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class TicketDelegation
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public int FromUserId { get; set; }

    public int ToUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User FromUser { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;

    public virtual User ToUser { get; set; } = null!;
}
