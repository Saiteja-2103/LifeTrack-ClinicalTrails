using AdverseEvent.API.DTOs;
using AdverseEvent.API.Models;
using Shared.CL.DTOs;
using AdverseEvent.API.Repositories.Interfaces;
using AdverseEvent.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Shared.CL.Exceptions;

namespace AdverseEvent.API.Services;

public class DeviationService : IDeviationService
{
    private readonly IDeviationRepository _repo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "deviations:list:version";
    private const string ItemPrefix = "deviations:item";

    public DeviationService(IDeviationRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<PagedResult<DeviationResponse>> ListAsync(
        long? siteProtocolId, string? severity, string? status, int page, int pageSize)
    {
        var v = GetVersion();
        var key = $"deviations:list:v{v}:{siteProtocolId}:{severity}:{status}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<DeviationResponse>? cached) && cached is not null)
            return cached;
        var (items, total) = await _repo.ListAsync(siteProtocolId, severity, status, page, pageSize);
        var result = new PagedResult<DeviationResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(ToResponse).ToList()
        };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<DeviationResponse?> GetAsync(long id)
    {
        var key = $"{ItemPrefix}:{id}";
        if (_cache.TryGetValue(key, out DeviationResponse? cached) && cached is not null)
            return cached;
        var d = await _repo.GetByIdAsync(id);
        if (d is null) return null;
        var r = ToResponse(d);
        _cache.Set(key, r, CacheDuration);
        return r;
    }

    public async Task<DeviationResponse> CreateAsync(CreateDeviationRequest req)
    {
        var deviation = new Deviation
        {
            SiteProtocolID = req.SiteProtocolID,
            Description = req.Description,
            Severity = req.Severity,
            Status = "Reported"
        };
        var created = await _repo.AddAsync(deviation);
        BumpVersion();
        return ToResponse(created);
    }

    public async Task<DeviationResponse?> UpdateAsync(long id, UpdateDeviationRequest req)
    {
        var d = await _repo.GetByIdAsync(id);
        if (d is null) return null;
        if (d.Status == "Closed" && req.Status != "Closed")
            throw new DomainException("A closed deviation cannot be reopened.");
        d.SiteProtocolID = req.SiteProtocolID;
        d.Description = req.Description;
        d.Severity = req.Severity;
        d.Status = req.Status;
        await _repo.UpdateAsync(d);
        Invalidate(id);
        return ToResponse(d);
    }

    private int GetVersion() =>
        _cache.GetOrCreate(VersionKey, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });

    private void BumpVersion() =>
        _cache.Set(VersionKey, GetVersion() + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    private void Invalidate(long id) { _cache.Remove($"{ItemPrefix}:{id}"); BumpVersion(); }

    private static DeviationResponse ToResponse(Deviation x) =>
        new(x.DeviationID, x.SiteProtocolID, x.Description, x.Severity, x.Status);
}
