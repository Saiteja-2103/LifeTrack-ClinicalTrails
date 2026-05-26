using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Patient.API.DTOs;
using Patient.API.Services.Interfaces;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;
namespace Patient.API.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patients;
    public PatientsController(IPatientService patients) => _patients = patients;

    [HttpGet]
    public async Task<ActionResult<PagedResult<PatientResponse>>> List(
        [FromQuery] string? enrollmentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _patients.ListAsync(enrollmentStatus, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PatientResponse>> Get(long id)
    {
        var p = await _patients.GetAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    /// <summary>
    /// Create a patient record.
    /// Admin / CTM / Investigator: can create for any patient, optionally setting UserID.
    /// Patient role: called automatically by Auth API during self-registration;
    ///               UserID is always overridden to the caller's own UserID.
    /// </summary>
    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.Investigator, RolesEnum.Patient)]
    public async Task<ActionResult<PatientResponse>> Create([FromBody] CreatePatientRequest req)
    {
        // When a patient role calls this endpoint, force UserID to their own account
        // so they cannot create records on behalf of other users.
        CreatePatientRequest finalReq = req;
        if (GetCallerRole() == nameof(RolesEnum.Patient))
        {
            var callerId = GetCallerId();
            finalReq = req with { UserID = callerId };
        }

        try
        {
            var created = await _patients.CreateAsync(finalReq);
            return CreatedAtAction(nameof(Get), new { id = created.PatientID }, created);
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.Investigator)]
    public async Task<ActionResult<PatientResponse>> Update(long id, [FromBody] UpdatePatientRequest req)
    {
        try
        {
            var updated = await _patients.UpdateAsync(id, req);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private long? GetCallerId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var id) ? id : null;
    }

    private string? GetCallerRole() => User.FindFirstValue(ClaimTypes.Role);
}
