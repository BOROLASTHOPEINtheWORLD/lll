using System;
using System.Collections.Generic;
namespace labsupport.Models;

public partial class TicketComment
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public int UserId { get; set; }

    public string Content { get; set; } = null!;

    public bool? IsInternal { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual ICollection<MessageAttachment> MessageAttachments { get; set; } = new List<MessageAttachment>();

    public virtual Ticket Ticket { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
