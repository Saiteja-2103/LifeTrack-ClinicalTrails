using Microsoft.Extensions.Caching.Memory;
using ProtocolSite.API.DTOs;
using ProtocolSite.API.Models;
using Shared.CL.DTOs;
using ProtocolSite.API.Repositories.Interfaces;
using ProtocolSite.API.Services.Interfaces;
using Shared.CL.Exceptions;
namespace ProtocolSite.API.Services;

public class ProtocolService : IProtocolService
{
    private readonly IProtocolRepository _repo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "protocols:list:version";
    private const string ItemPrefix = "protocols:item";

    public ProtocolService(IProtocolRepository repo, IMemoryCache cache)
    { _repo = repo; _cache = cache; }

    public async Task<ProtocolResponse?> GetAsync(long id)
    {
        var key = $"{ItemPrefix}:{id}";
        if (_cache.TryGetValue(key, out ProtocolResponse? cached) && cached is not null) return cached;
        var p = await _repo.GetByIdAsync(id);
        if (p is null) return null;
        var r = Map(p); _cache.Set(key, r, CacheDuration); return r;
    }

    public async Task<PagedResult<ProtocolResponse>> ListAsync(string? status, string? phase, string? search, int page, int pageSize)
    {
        var v = GetVersion();
        var key = $"protocols:list:v{v}:{status}:{phase}:{search}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<ProtocolResponse>? cached) && cached is not null) return cached;
        var (items, total) = await _repo.ListAsync(status, phase, search, page, pageSize);
        var result = new PagedResult<ProtocolResponse> { Page = page, PageSize = pageSize, TotalCount = total, Items = items.Select(Map).ToList() };
        _cache.Set(key, result, CacheDuration); return result;
    }

    public async Task<ProtocolResponse> CreateAsync(CreateProtocolRequest req)
    {
        if (req.EndDate.HasValue && req.EndDate.Value < req.StartDate) throw new DomainException("EndDate cannot be earlier than StartDate.");
        var protocol = new Protocol { Title = req.Title.Trim(), Phase = req.Phase, StartDate = req.StartDate, EndDate = req.EndDate, Status = req.Status };
        await _repo.AddAsync(protocol); BumpVersion(); return Map(protocol);
    }

    public async Task<ProtocolResponse?> UpdateAsync(long id, UpdateProtocolRequest req)
    {
        var protocol = await _repo.GetByIdAsync(id); if (protocol is null) return null;
        if (req.EndDate.HasValue && req.EndDate.Value < req.StartDate) throw new DomainException("EndDate cannot be earlier than StartDate.");
        protocol.Title = req.Title.Trim(); protocol.Phase = req.Phase; protocol.StartDate = req.StartDate; protocol.EndDate = req.EndDate; protocol.Status = req.Status;
        await _repo.UpdateAsync(protocol); Invalidate(id); return Map(protocol);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var protocol = await _repo.GetByIdAsync(id); if (protocol is null) return false;
        if (protocol.Status == "Active") throw new DomainException("Cannot delete an active protocol. Pause or terminate it first.");
        await _repo.DeleteAsync(id); Invalidate(id); return true;
    }

    private int GetVersion() => _cache.GetOrCreate(VersionKey, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });
    private void BumpVersion() => _cache.Set(VersionKey, GetVersion() + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    private void Invalidate(long id) { _cache.Remove($"{ItemPrefix}:{id}"); BumpVersion(); }

    private static ProtocolResponse Map(Protocol p) => new() { ProtocolID = p.ProtocolID, Title = p.Title, Phase = p.Phase, StartDate = p.StartDate, EndDate = p.EndDate, Status = p.Status };
}
