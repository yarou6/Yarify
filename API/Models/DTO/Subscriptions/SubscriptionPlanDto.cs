namespace API.Models.DTO;

public sealed class SubscriptionPlanDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsFree { get; set; }
    public string? Description { get; set; }
    public decimal? MonthlyPrice { get; set; }
    public string? Currency { get; set; }
}
