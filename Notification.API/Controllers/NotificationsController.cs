using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Notification.API.DTOs;
using Notification.API.Services.Interfaces;
using Shared.CL.Enums;
using Shared.CL.Filters;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _svc;

    public NotificationsController(INotificationService svc) => _svc = svc;

    /// <summary>Get notifications for the calling user.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> ListMine(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _svc.ListForUserAsync(userId.Value, status, category, page, pageSize));
    }

    /// <summary>List all notifications across all users. Admin / ClinicalTrialManager only.</summary>
    [HttpGet]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> ListAll(
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _svc.ListAllAsync(category, status, page, pageSize));
    }

    /// <summary>Create (send) a notification. Admin / ClinicalTrialManager only.</summary>
    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<NotificationResponse>> Create([FromBody] CreateNotificationRequest req)
    {
        var created = await _svc.CreateAsync(req);
        return CreatedAtAction(nameof(ListMine), null, created);
    }

    /// <summary>Mark a single notification as read. Caller must own the notification.</summary>
    [HttpPost("{id:long}/read")]
    public async Task<ActionResult<NotificationResponse>> MarkRead(long id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _svc.MarkReadAsync(id, userId.Value);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        return long.TryParse(claim, out var id) ? id : null;
    }
}
