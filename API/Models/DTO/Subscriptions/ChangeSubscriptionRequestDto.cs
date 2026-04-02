using System.ComponentModel.DataAnnotations;

namespace API.Models.DTO;

public sealed class ChangeSubscriptionRequestDto
{
    [Range(1, int.MaxValue)]
    public int PlanId { get; set; }

    public bool IsAutoRenew { get; set; }
}
