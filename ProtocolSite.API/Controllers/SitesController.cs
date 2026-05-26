using Microsoft.AspNetCore.Mvc;
using ProtocolSite.API.DTOs;
using ProtocolSite.API.Services.Interfaces;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;
namespace ProtocolSite.API.Controllers;

[ApiController]
[Route("api/sites")]
public class SitesController : ControllerBase
{
    private readonly ISiteService _sites;
    public SitesController(ISiteService sites) => _sites = sites;

    [HttpGet]
    public async Task<ActionResult<PagedResult<SiteResponse>>> List(
        [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1; if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _sites.ListAsync(status, search, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SiteResponse>> Get(long id)
    { var s = await _sites.GetAsync(id); return s is null ? NotFound() : Ok(s); }

    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<SiteResponse>> Create([FromBody] CreateSiteRequest req)
    { var c = await _sites.CreateAsync(req); return CreatedAtAction(nameof(Get), new { id = c.SiteID }, c); }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<SiteResponse>> Update(long id, [FromBody] UpdateSiteRequest req)
    { var u = await _sites.UpdateAsync(id, req); return u is null ? NotFound() : Ok(u); }

    [HttpDelete("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin)]
    public async Task<IActionResult> Delete(long id)
    {
        try { return await _sites.DeleteAsync(id) ? NoContent() : NotFound(); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
