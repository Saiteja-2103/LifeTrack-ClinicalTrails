using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProtocolSite.API.Data;
using ProtocolSite.API.DTOs;
using Shared.CL.DTOs;
using Shared.CL.Enums;
using Shared.CL.Filters;

namespace ProtocolSite.API.Controllers;

/// <summary>
/// Exposes the entity-level audit trail stored in lifetrack_clinical_db.dbo.AuditEntries.
/// Because all clinical microservices share the same database, this endpoint returns
/// audit entries for ALL clinical entities (Protocols, Sites, Patients, Enrollments, etc.).
/// Admin and RegulatoryOfficer may call this endpoint.
/// </summary>
[ApiController]
[Route("api/audit-entries")]
[RoleAuthorize(RolesEnum.Admin, RolesEnum.RegulatoryOfficer)]
public class AuditEntriesController : ControllerBase
{
    private readonly ProtocolSiteDbContext _db;
    public AuditEntriesController(ProtocolSiteDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> List(
        [FromQuery] string? entity,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize is <= 0 or > 1000) pageSize = 50;

        var query = _db.AuditEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(a => a.EntityName.Contains(entity));

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditEntryDto
            {
                AuditEntryID = a.AuditEntryID,
                EntityName = a.EntityName,
                PrimaryKey = a.PrimaryKey,
                Action = a.Action,
                ChangedByUserID = a.ChangedByUserID,
                ChangedAt = a.ChangedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<AuditEntryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        });
    }
}
