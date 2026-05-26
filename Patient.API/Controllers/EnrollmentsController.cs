using Microsoft.AspNetCore.Mvc;
using Patient.API.DTOs;
using Patient.API.Services.Interfaces;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;
namespace Patient.API.Controllers;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _svc;
    public EnrollmentsController(IEnrollmentService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<PagedResult<EnrollmentResponse>>> List(
        [FromQuery] long? patientId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _svc.ListAsync(patientId, status, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<EnrollmentResponse>> Get(long id)
    {
        var result = await _svc.GetAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.Investigator)]
    public async Task<ActionResult<EnrollmentResponse>> Create([FromBody] CreateEnrollmentRequest req)
    {
        try { var created = await _svc.CreateAsync(req); return CreatedAtAction(nameof(Get), new { id = created.EnrollmentID }, created); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.Investigator)]
    public async Task<ActionResult<EnrollmentResponse>> Update(long id, [FromBody] UpdateEnrollmentRequest req)
    {
        try { var updated = await _svc.UpdateAsync(id, req); return updated is null ? NotFound() : Ok(updated); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
