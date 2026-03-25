using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Subscriptionplan
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public bool IsFree { get; set; }

    public string? Description { get; set; }

    public decimal? MonthlyPrice { get; set; }

    public string? Currency { get; set; }

    public virtual ICollection<Userplan> Userplans { get; set; } = new List<Userplan>();
}

