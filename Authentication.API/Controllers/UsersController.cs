using System.Security.Claims;
using Authentication.API.DTOs;
using Authentication.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.CL.Enums;
using Shared.CL.Filters;
namespace Authentication.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase 
{
    private readonly IUserService _users;
    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 100) pageSize = 10;
        return Ok(await _users.ListAsync(page, pageSize, search));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserResponse>> Get(long id)
    {
        var user = await _users.GetAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin)]
    public async Task<ActionResult<UserResponse>> Update(long id, [FromBody] UpdateUserRequest req)
    {
        var actingId = GetUserId() ?? 0;
        try
        {
            var user = await _users.UpdateAsync(id, req, actingId);
            return user is null ? NotFound() : Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin)]
    public async Task<IActionResult> Delete(long id)
    {
        var actingId = GetUserId() ?? 0;
        if (actingId == id) return BadRequest(new { error = "Cannot delete your own account." });
        if (id == 1) return BadRequest(new { error = "The Super Admin account cannot be deleted." });
        try
        {
            return await _users.DeleteAsync(id, actingId) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:long}/reactivate")]
    [RoleAuthorize(RolesEnum.Admin)]
    public async Task<IActionResult> Reactivate(long id)
    {
        var actingId = GetUserId() ?? 0;
        var result = await _users.ReactivateAsync(id, actingId);
        return result is null ? NotFound() : Ok(result);
    }

    private long? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var id) ? id : null;
    }
}
