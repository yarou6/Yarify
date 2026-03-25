using System;
using System.Collections.Generic;

namespace API.DB;

public partial class Userplan
{
    public int UserId { get; set; }

    public int PlanId { get; set; }

    public string Status { get; set; } = null!;

    public bool? IsActive { get; set; }

    public bool IsAutoRenew { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? NextRenewAt { get; set; }

    public virtual Subscriptionplan Plan { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

