using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class MessageAttachment
{
    public long Id { get; set; }

    public long CommentId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public DateTime? UploadedAt { get; set; }

    public virtual TicketComment Comment { get; set; } = null!;
}
