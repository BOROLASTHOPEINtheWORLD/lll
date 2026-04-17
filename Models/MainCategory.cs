using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class MainCategory
{
    public short Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
