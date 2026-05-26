using Microsoft.AspNetCore.Mvc;
using ProtocolSite.API.DTOs;
using ProtocolSite.API.Services.Interfaces;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;
namespace ProtocolSite.API.Controllers;

[ApiController]
[Route("api/site-protocols")]
public class SiteProtocolsController : ControllerBase
{
    private readonly ISiteProtocolService _svc;
    public SiteProtocolsController(ISiteProtocolService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<PagedResult<SiteProtocolResponse>>> List(
        [FromQuery] long? siteId, [FromQuery] long? protocolId, [FromQuery] long? investigatorId,
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1; if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _svc.ListAsync(siteId, protocolId, investigatorId, status, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SiteProtocolResponse>> Get(long id)
    { var r = await _svc.GetAsync(id); return r is null ? NotFound() : Ok(r); }

    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<SiteProtocolResponse>> Create([FromBody] CreateSiteProtocolRequest req)
    {
        try { var c = await _svc.CreateAsync(req); return CreatedAtAction(nameof(Get), new { id = c.SiteProtocolID }, c); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager)]
    public async Task<ActionResult<SiteProtocolResponse>> Update(long id, [FromBody] UpdateSiteProtocolRequest req)
    {
        try { var u = await _svc.UpdateAsync(id, req); return u is null ? NotFound() : Ok(u); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
