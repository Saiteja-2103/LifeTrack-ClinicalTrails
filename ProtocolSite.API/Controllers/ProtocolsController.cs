using Microsoft.AspNetCore.Mvc;
using ProtocolSite.API.DTOs;
using ProtocolSite.API.Services.Interfaces;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;
namespace ProtocolSite.API.Controllers;

[ApiController]
[Route("api/protocols")]
public class ProtocolsController : ControllerBase
{
    private readonly IProtocolService _protocols;
    public ProtocolsController(IProtocolService protocols) => _protocols = protocols;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProtocolResponse>>> List(
        [FromQuery] string? status, [FromQuery] string? phase,
        [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1; if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _protocols.ListAsync(status, phase, search, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProtocolResponse>> Get(long id)
    { var p = await _protocols.GetAsync(id); return p is null ? NotFound() : Ok(p); }

    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<ProtocolResponse>> Create([FromBody] CreateProtocolRequest req)
    {
        try { var c = await _protocols.CreateAsync(req); return CreatedAtAction(nameof(Get), new { id = c.ProtocolID }, c); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<ProtocolResponse>> Update(long id, [FromBody] UpdateProtocolRequest req)
    {
        try { var u = await _protocols.UpdateAsync(id, req); return u is null ? NotFound() : Ok(u); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin)]
    public async Task<IActionResult> Delete(long id)
    {
        try { return await _protocols.DeleteAsync(id) ? NoContent() : NotFound(); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
