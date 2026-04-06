namespace MVVM.Models.Subscriptions;

public sealed class ChangeSubscriptionRequestDto
{
    public int PlanId { get; set; }
    public bool IsAutoRenew { get; set; }
}
