using AdverseEvent.API.DTOs;
using AdverseEvent.API.Models;
using Shared.CL.DTOs;
using AdverseEvent.API.Repositories.Interfaces;
using AdverseEvent.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Shared.CL.Exceptions;

namespace AdverseEvent.API.Services;

public class AdverseEventService : IAdverseEventService
{
    private readonly IAdverseEventRepository _repo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "ae:list:version";
    private const string ItemPrefix = "ae:item";

    public AdverseEventService(IAdverseEventRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<PagedResult<AdverseEventResponse>> ListAsync(
        long? protocolId, long? patientId, string? severity, string? status, int page, int pageSize)
    {
        var v = GetVersion();
        var key = $"ae:list:v{v}:{protocolId}:{patientId}:{severity}:{status}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<AdverseEventResponse>? cached) && cached is not null)
            return cached;
        var (items, total) = await _repo.ListAsync(protocolId, patientId, severity, status, page, pageSize);
        var result = new PagedResult<AdverseEventResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(ToResponse).ToList()
        };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<AdverseEventResponse?> GetAsync(long id)
    {
        var key = $"{ItemPrefix}:{id}";
        if (_cache.TryGetValue(key, out AdverseEventResponse? cached) && cached is not null)
            return cached;
        var ae = await _repo.GetByIdAsync(id);
        if (ae is null) return null;
        var r = ToResponse(ae);
        _cache.Set(key, r, CacheDuration);
        return r;
    }

    public async Task<AdverseEventResponse> CreateAsync(CreateAdverseEventRequest req)
    {
        var ae = new AdverseEventRecord
        {
            PatientID = req.PatientID,
            ProtocolID = req.ProtocolID,
            Description = req.Description,
            Severity = req.Severity,
            Status = "Reported",
            ReportedDate = req.ReportedDate
        };
        var created = await _repo.AddAsync(ae);
        BumpVersion();
        return ToResponse(created);
    }

    public async Task<AdverseEventResponse?> UpdateAsync(long id, UpdateAdverseEventRequest req)
    {
        var ae = await _repo.GetByIdAsync(id);
        if (ae is null) return null;
        if (ae.Status == "Closed" && req.Status != "Closed")
            throw new DomainException("A closed adverse event cannot be reopened.");
        ae.PatientID = req.PatientID;
        ae.ProtocolID = req.ProtocolID;
        ae.Description = req.Description;
        ae.Severity = req.Severity;
        ae.Status = req.Status;
        ae.ReportedDate = req.ReportedDate;
        await _repo.UpdateAsync(ae);
        Invalidate(id);
        return ToResponse(ae);
    }

    private int GetVersion() =>
        _cache.GetOrCreate(VersionKey, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });

    private void BumpVersion() =>
        _cache.Set(VersionKey, GetVersion() + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    private void Invalidate(long id) { _cache.Remove($"{ItemPrefix}:{id}"); BumpVersion(); }

    private static AdverseEventResponse ToResponse(AdverseEventRecord x) =>
        new(x.EventID, x.PatientID, x.ProtocolID, x.Description, x.Severity, x.Status, x.ReportedDate);
}
