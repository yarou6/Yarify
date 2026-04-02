using API.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.Extensions;

public static class ApiErrorExtensions
{
    public static IActionResult ApiError(this ControllerBase controller, int statusCode, string message)
    {
        return controller.StatusCode(statusCode, new ApiErrorResponse
        {
            Message = message
        });
    }

    public static IActionResult ApiValidationError(this ControllerBase controller, ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());

        return controller.BadRequest(new ApiErrorResponse
        {
            Message = "Validation failed.",
            Errors = errors
        });
    }
}
