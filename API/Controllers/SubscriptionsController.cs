using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContext;

    public SubscriptionsController(YarifyDbContext db, IUserContextService userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    // Готовит и возвращает нужные данные.
    public async Task<ActionResult<List<SubscriptionPlanDto>>> GetPlans()
    {
        var plans = await _db.Subscriptionplans
            .AsNoTracking()
            .OrderBy(p => p.IsFree ? 0 : 1)
            .ThenBy(p => p.MonthlyPrice)
            .Select(p => new SubscriptionPlanDto
            {
                Id = p.Id,
                Title = p.Title,
                IsFree = p.IsFree,
                Description = p.Description,
                MonthlyPrice = p.MonthlyPrice,
                Currency = p.Currency
            })
            .ToListAsync();

        return Ok(plans);
    }

    [HttpGet("me")]
    // Готовит и возвращает нужные данные.
    public async Task<ActionResult<UserSubscriptionDto>> GetMySubscription()
    {
        var userId = _userContext.GetRequiredUserId(User);
        var subscription = await EnsureUserSubscriptionAsync(userId);
        return Ok(subscription);
    }

    [HttpPut("me")]
    // Выполняет внутреннюю логику метода.
    public async Task<ActionResult<UserSubscriptionDto>> ChangeMySubscription([FromBody] ChangeSubscriptionRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var plan = await _db.Subscriptionplans.FirstOrDefaultAsync(p => p.Id == request.PlanId);
        if (plan is null)
            return NotFound(new ApiErrorResponse { Message = "План подписки не найден." });

        var userPlan = await _db.Userplans.FirstOrDefaultAsync(up => up.UserId == userId);
        if (userPlan is null)
        {
            userPlan = new Userplan
            {
                UserId = userId,
                PlanId = plan.Id,
                Status = "Active",
                IsActive = true,
                IsAutoRenew = request.IsAutoRenew,
                StartedAt = DateTime.UtcNow,
                ExpiresAt = plan.IsFree ? null : DateTime.UtcNow.AddDays(30),
                NextRenewAt = request.IsAutoRenew && !plan.IsFree ? DateTime.UtcNow.AddDays(30) : null
            };
            _db.Userplans.Add(userPlan);
        }
        else
        {
            userPlan.PlanId = plan.Id;
            userPlan.Status = "Active";
            userPlan.IsActive = true;
            userPlan.IsAutoRenew = request.IsAutoRenew;

            if (plan.IsFree)
            {
                userPlan.ExpiresAt = null;
                userPlan.NextRenewAt = null;
            }
            else
            {
                userPlan.ExpiresAt = DateTime.UtcNow.AddDays(30);
                userPlan.NextRenewAt = request.IsAutoRenew ? DateTime.UtcNow.AddDays(30) : null;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(await EnsureUserSubscriptionAsync(userId));
    }

    [HttpPost("me/cancel")]
    // Проверяет условие и возвращает результат проверки.
    public async Task<ActionResult<UserSubscriptionDto>> CancelMySubscription()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var userPlan = await _db.Userplans.FirstOrDefaultAsync(up => up.UserId == userId);
        if (userPlan is null)
            return NotFound(new ApiErrorResponse { Message = "Подписка пользователя не найдена." });

        userPlan.Status = "Canceled";
        userPlan.IsActive = false;
        userPlan.IsAutoRenew = false;
        userPlan.NextRenewAt = null;

        await _db.SaveChangesAsync();

        return Ok(await EnsureUserSubscriptionAsync(userId));
    }

    [HttpPost("me/resume")]
    // Выполняет внутреннюю логику метода.
    public async Task<ActionResult<UserSubscriptionDto>> ResumeMySubscription([FromQuery] bool autoRenew = true)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var userPlan = await _db.Userplans
            .Include(up => up.Plan)
            .FirstOrDefaultAsync(up => up.UserId == userId);

        if (userPlan is null)
            return NotFound(new ApiErrorResponse { Message = "Подписка пользователя не найдена." });

        userPlan.Status = "Active";
        userPlan.IsActive = true;
        userPlan.IsAutoRenew = autoRenew;

        if (userPlan.Plan.IsFree)
        {
            userPlan.ExpiresAt = null;
            userPlan.NextRenewAt = null;
        }
        else
        {
            userPlan.ExpiresAt ??= DateTime.UtcNow.AddDays(30);
            userPlan.NextRenewAt = autoRenew ? userPlan.ExpiresAt : null;
        }

        await _db.SaveChangesAsync();

        return Ok(await EnsureUserSubscriptionAsync(userId));
    }

    // Выполняет внутреннюю логику метода.
    private async Task<UserSubscriptionDto> EnsureUserSubscriptionAsync(int userId)
    {
        var userPlan = await _db.Userplans
            .Include(up => up.Plan)
            .FirstOrDefaultAsync(up => up.UserId == userId);

        if (userPlan is null)
        {
            var freePlan = await _db.Subscriptionplans.FirstOrDefaultAsync(p => p.IsFree) ??
                           await _db.Subscriptionplans.OrderBy(p => p.Id).FirstAsync();

            userPlan = new Userplan
            {
                UserId = userId,
                PlanId = freePlan.Id,
                Status = "Active",
                IsActive = true,
                IsAutoRenew = false,
                StartedAt = DateTime.UtcNow,
                ExpiresAt = null,
                NextRenewAt = null
            };

            _db.Userplans.Add(userPlan);
            await _db.SaveChangesAsync();

            userPlan = await _db.Userplans.Include(up => up.Plan).FirstAsync(up => up.UserId == userId);
        }

        return new UserSubscriptionDto
        {
            UserId = userPlan.UserId,
            PlanId = userPlan.PlanId,
            PlanTitle = userPlan.Plan.Title,
            Status = userPlan.Status,
            IsActive = userPlan.IsActive ?? true,
            IsAutoRenew = userPlan.IsAutoRenew,
            StartedAt = userPlan.StartedAt,
            ExpiresAt = userPlan.ExpiresAt,
            NextRenewAt = userPlan.NextRenewAt
        };
    }
}
