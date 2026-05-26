using DocumentCompliance.API.DTOs;
using DocumentCompliance.API.Services.Interfaces;
using Shared.CL.DTOs;
using Microsoft.AspNetCore.Mvc;
using Shared.CL.Enums;
using Shared.CL.Exceptions;
using Shared.CL.Filters;

namespace DocumentCompliance.API.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _svc;

    public DocumentsController(IDocumentService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> List(
        [FromQuery] long? protocolId,
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 200) pageSize = 20;
        return Ok(await _svc.ListAsync(protocolId, status, type, page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentResponse>> Get(long id)
    {
        var doc = await _svc.GetAsync(id);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpPost]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.RegulatoryOfficer)]
    public async Task<ActionResult<DocumentResponse>> Create([FromBody] CreateDocumentRequest req)
    {
        var created = await _svc.CreateAsync(req);
        return CreatedAtAction(nameof(Get), new { id = created.DocumentID }, created);
    }

    [HttpPut("{id:long}")]
    [RoleAuthorize(RolesEnum.Admin, RolesEnum.ClinicalTrialManager, RolesEnum.RegulatoryOfficer)]
    public async Task<ActionResult<DocumentResponse>> Update(long id, [FromBody] UpdateDocumentRequest req)
    {
        try
        {
            var updated = await _svc.UpdateAsync(id, req);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

}
