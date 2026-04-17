using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class TicketStatus
{
    public short Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
