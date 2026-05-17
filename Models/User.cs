using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class User
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public short RoleId { get; set; }

    public short? DepartmentId { get; set; }

    public short? PositionId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? MiddleName { get; set; }

    public string? Phone { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? Username { get; set; }

    public string? AvatarPath { get; set; }

    public virtual Department? Department { get; set; }

    public virtual Position? Position { get; set; }

    public virtual Role Role { get; set; } = null!;
    public DateTime? LastSeenAt { get; set; }

    public virtual ICollection<Ticket> TicketAssignedTos { get; set; } = new List<Ticket>();

    public virtual ICollection<TicketComment> TicketComments { get; set; } = new List<TicketComment>();

    public virtual ICollection<Ticket> TicketCreatedBies { get; set; } = new List<Ticket>();

    public virtual ICollection<TicketDelegation> TicketDelegationFromUsers { get; set; } = new List<TicketDelegation>();

    public virtual ICollection<TicketDelegation> TicketDelegationToUsers { get; set; } = new List<TicketDelegation>();

    public virtual ICollection<TicketHistory> TicketHistories { get; set; } = new List<TicketHistory>();
    public virtual ICollection<TicketPriorityChange> TicketPriorityChanges { get; set; } = new List<TicketPriorityChange>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
