using System;
using System.Collections.Generic;
namespace labsupport.Models;

public partial class Ticket
{
    public long Id { get; set; }

    public string TicketNumber { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public short StatusId { get; set; }

    public short? CategoryId { get; set; }

    public int CreatedById { get; set; }

    public int AssignedToId { get; set; }

    public short Priority { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Resolution { get; set; }

    public DateTime? DueDate { get; set; }

    public virtual User AssignedTo { get; set; } = null!;

    public virtual MainCategory? Category { get; set; }

    public virtual User CreatedBy { get; set; } = null!;

    public virtual SatisfactionRating? SatisfactionRating { get; set; }

    public virtual TicketStatus Status { get; set; } = null!;

    public virtual ICollection<TicketAttachment> TicketAttachments { get; set; } = new List<TicketAttachment>();

    public virtual ICollection<TicketComment> TicketComments { get; set; } = new List<TicketComment>();

    public virtual ICollection<TicketDelegation> TicketDelegations { get; set; } = new List<TicketDelegation>();

    public virtual ICollection<TicketHistory> TicketHistories { get; set; } = new List<TicketHistory>();
}
