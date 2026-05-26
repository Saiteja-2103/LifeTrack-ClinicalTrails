using Microsoft.AspNetCore.Mvc;
using Patient.API.DTOs;
using Patient.API.Services.Interfaces;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;
namespace Patient.API.Controllers;

[ApiController]
[Route("api/visits")]
public class VisitsController : ControllerBase
{
    private readonly IVisitService _visits;
    public VisitsController(IVisitService visits) => _visits = visits;

    [HttpGet]
    public async Task<ActionResult<PagedResult<VisitResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] long? enrollmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _visits.ListAsync(status, enrollmentId, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<VisitResponse>> Get(long id)
    {
        var v = await _visits.GetAsync(id);
        return v is null ? NotFound() : Ok(v);
    }

    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.Investigator)]
    public async Task<ActionResult<VisitResponse>> Create([FromBody] CreateVisitRequest req)
    {
        try { var created = await _visits.CreateAsync(req); return CreatedAtAction(nameof(Get), new { id = created.VisitID }, created); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.Investigator)]
    public async Task<ActionResult<VisitResponse>> Update(long id, [FromBody] UpdateVisitRequest req)
    {
        try { var updated = await _visits.UpdateAsync(id, req); return updated is null ? NotFound() : Ok(updated); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
