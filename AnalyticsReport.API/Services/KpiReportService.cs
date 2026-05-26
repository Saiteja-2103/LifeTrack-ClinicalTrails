using AnalyticsReport.API.DTOs;
using AnalyticsReport.API.Models;
using Shared.CL.DTOs;
using AnalyticsReport.API.Repositories.Interfaces;
using AnalyticsReport.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace AnalyticsReport.API.Services;

public class KpiReportService : IKpiReportService
{
    private readonly IKpiReportRepository _repo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "kpi:list:version";
    private const string ItemPrefix = "kpi:item";

    public KpiReportService(IKpiReportRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<PagedResult<KpiReportResponse>> ListAsync(string? scope, int page, int pageSize)
    {
        var v = GetVersion();
        var key = $"kpi:list:v{v}:{scope}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<KpiReportResponse>? cached) && cached is not null)
            return cached;
        var (items, total) = await _repo.ListAsync(scope, page, pageSize);
        var result = new PagedResult<KpiReportResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(ToResponse).ToList()
        };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<KpiReportResponse?> GetAsync(long id)
    {
        var key = $"{ItemPrefix}:{id}";
        if (_cache.TryGetValue(key, out KpiReportResponse? cached) && cached is not null)
            return cached;
        var r = await _repo.GetByIdAsync(id);
        if (r is null) return null;
        var response = ToResponse(r);
        _cache.Set(key, response, CacheDuration);
        return response;
    }

    public async Task<KpiReportResponse> CreateAsync(CreateKpiReportRequest req)
    {
        var report = new KpiReport
        {
            Scope = req.Scope,
            EnrollmentRate = req.EnrollmentRate,
            DropoutRate = req.DropoutRate,
            AECount = req.AECount,
            GeneratedDate = req.GeneratedDate ?? DateTime.UtcNow
        };
        var created = await _repo.AddAsync(report);
        BumpVersion();
        return ToResponse(created);
    }

    private int GetVersion() =>
        _cache.GetOrCreate(VersionKey, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });

    private void BumpVersion() =>
        _cache.Set(VersionKey, GetVersion() + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    private static KpiReportResponse ToResponse(KpiReport r) =>
        new(r.ReportID, r.Scope, r.EnrollmentRate, r.DropoutRate, r.AECount, r.GeneratedDate);
}
