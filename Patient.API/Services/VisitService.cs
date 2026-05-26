using Microsoft.Extensions.Caching.Memory;
using Patient.API.DTOs;
using Patient.API.Models;
using Shared.CL.DTOs;
using Patient.API.Repositories.Interfaces;
using Patient.API.Services.Interfaces;
using Shared.CL.Exceptions;
namespace Patient.API.Services;

public class VisitService : IVisitService
{
    private readonly IVisitRepository _visits;
    private readonly IEnrollmentRepository _enrollments;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "visits:list:version";
    private const string ItemPrefix = "visits:item";

    public VisitService(IVisitRepository visits, IEnrollmentRepository enrollments, IMemoryCache cache)
    { _visits = visits; _enrollments = enrollments; _cache = cache; }

    public async Task<PagedResult<VisitResponse>> ListAsync(string? status, long? enrollmentId, int page, int pageSize)
    {
        var v = GetVersion(VersionKey);
        var key = $"visits:list:v{v}:{status}:{enrollmentId}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<VisitResponse>? cached) && cached is not null) return cached;
        var (items, total) = await _visits.ListAsync(status, enrollmentId, page, pageSize);
        var result = new PagedResult<VisitResponse> { Page = page, PageSize = pageSize, TotalCount = total, Items = items.Select(ToResponse).ToList() };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<VisitResponse?> GetAsync(long visitId)
    {
        var key = $"{ItemPrefix}:{visitId}";
        if (_cache.TryGetValue(key, out VisitResponse? cached) && cached is not null) return cached;
        var v = await _visits.GetByIdAsync(visitId);
        if (v is null) return null;
        var r = ToResponse(v);
        _cache.Set(key, r, CacheDuration);
        return r;
    }

    public async Task<VisitResponse> CreateAsync(CreateVisitRequest req)
    {
        var enrollment = await _enrollments.GetByIdAsync(req.EnrollmentID);
        if (enrollment is null) throw new DomainException($"Enrollment {req.EnrollmentID} not found.");
        if (enrollment.Status == "Withdrawn") throw new DomainException("Cannot add visits to a withdrawn enrollment.");
        var visit = new Visit { EnrollmentID = req.EnrollmentID, VisitDate = req.VisitDate, Status = req.Status, Notes = req.Notes ?? string.Empty };
        BumpVersion(VersionKey);
        return ToResponse(await _visits.AddAsync(visit));
    }

    public async Task<VisitResponse?> UpdateAsync(long visitId, UpdateVisitRequest req)
    {
        var visit = await _visits.GetByIdAsync(visitId);
        if (visit is null) return null;
        if (visit.Status == "Completed" && req.Status != "Completed") throw new DomainException("A completed visit cannot be reopened.");
        visit.EnrollmentID = req.EnrollmentID; visit.VisitDate = req.VisitDate; visit.Status = req.Status; visit.Notes = req.Notes ?? string.Empty;
        await _visits.UpdateAsync(visit);
        Invalidate(ItemPrefix, visitId, VersionKey);
        return ToResponse(visit);
    }

    private int GetVersion(string key) => _cache.GetOrCreate(key, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });
    private void BumpVersion(string key) => _cache.Set(key, GetVersion(key) + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    private void Invalidate(string itemPrefix, long id, string versionKey) { _cache.Remove($"{itemPrefix}:{id}"); BumpVersion(versionKey); }

    private static VisitResponse ToResponse(Visit v) => new(v.VisitID, v.EnrollmentID, v.VisitDate, v.Status, v.Notes);
}
