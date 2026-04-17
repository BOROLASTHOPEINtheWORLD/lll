using System;
using System.Collections.Generic;

namespace labsupport.Models;

public partial class Subcategory
{
    public short Id { get; set; }

    public short MainCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public virtual MainCategory MainCategory { get; set; } = null!;
}
