using System;
using System.Collections.Generic;

namespace labsupport.Models;
public partial class TicketAttachment
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public DateTime? UploadedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
}
