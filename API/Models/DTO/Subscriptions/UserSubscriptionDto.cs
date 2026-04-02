namespace API.Models.DTO;

public sealed class UserSubscriptionDto
{
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public string PlanTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAutoRenew { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? NextRenewAt { get; set; }
}
