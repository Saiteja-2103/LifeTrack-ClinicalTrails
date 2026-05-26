using Microsoft.Extensions.Caching.Memory;
using ProtocolSite.API.DTOs;
using ProtocolSite.API.Models;
using Shared.CL.DTOs;
using ProtocolSite.API.Repositories.Interfaces;
using ProtocolSite.API.Services.Interfaces;
using Shared.CL.Exceptions;
namespace ProtocolSite.API.Services;

public class SiteService : ISiteService
{
    private readonly ISiteRepository _repo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "sites:list:version";
    private const string ItemPrefix = "sites:item";

    public SiteService(ISiteRepository repo, IMemoryCache cache)
    { _repo = repo; _cache = cache; }

    public async Task<SiteResponse?> GetAsync(long id)
    {
        var key = $"{ItemPrefix}:{id}";
        if (_cache.TryGetValue(key, out SiteResponse? cached) && cached is not null) return cached;
        var s = await _repo.GetByIdAsync(id); if (s is null) return null;
        var r = Map(s); _cache.Set(key, r, CacheDuration); return r;
    }

    public async Task<PagedResult<SiteResponse>> ListAsync(string? status, string? search, int page, int pageSize)
    {
        var v = GetVersion(); var key = $"sites:list:v{v}:{status}:{search}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<SiteResponse>? cached) && cached is not null) return cached;
        var (items, total) = await _repo.ListAsync(status, search, page, pageSize);
        var result = new PagedResult<SiteResponse> { Page = page, PageSize = pageSize, TotalCount = total, Items = items.Select(Map).ToList() };
        _cache.Set(key, result, CacheDuration); return result;
    }

    public async Task<SiteResponse> CreateAsync(CreateSiteRequest req)
    {
        var site = new Site { Name = req.Name.Trim(), Location = req.Location.Trim(), Status = req.Status };
        await _repo.AddAsync(site); BumpVersion(); return Map(site);
    }

    public async Task<SiteResponse?> UpdateAsync(long id, UpdateSiteRequest req)
    {
        var site = await _repo.GetByIdAsync(id); if (site is null) return null;
        site.Name = req.Name.Trim(); site.Location = req.Location.Trim(); site.Status = req.Status;
        await _repo.UpdateAsync(site); Invalidate(id); return Map(site);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var site = await _repo.GetByIdAsync(id); if (site is null) return false;
        if (site.Status == "Active") throw new DomainException("Cannot delete an active site. Suspend or close it first.");
        await _repo.DeleteAsync(id); Invalidate(id); return true;
    }

    private int GetVersion() => _cache.GetOrCreate(VersionKey, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });
    private void BumpVersion() => _cache.Set(VersionKey, GetVersion() + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    private void Invalidate(long id) { _cache.Remove($"{ItemPrefix}:{id}"); BumpVersion(); }

    private static SiteResponse Map(Site s) => new() { SiteID = s.SiteID, Name = s.Name, Location = s.Location, Status = s.Status };
}
