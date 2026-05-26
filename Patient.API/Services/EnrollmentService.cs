using Microsoft.Extensions.Caching.Memory;
using Patient.API.DTOs;
using Patient.API.Models;
using Shared.CL.DTOs;
using Patient.API.Repositories.Interfaces;
using Patient.API.Services.Interfaces;
using Shared.CL.Exceptions;
namespace Patient.API.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly IEnrollmentRepository _enrollments;
    private readonly IPatientRepository _patients;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "enrollments:list:version";
    private const string ItemPrefix = "enrollments:item";

    public EnrollmentService(IEnrollmentRepository enrollments, IPatientRepository patients, IMemoryCache cache)
    { _enrollments = enrollments; _patients = patients; _cache = cache; }

    public async Task<EnrollmentResponse?> GetAsync(long enrollmentId)
    {
        var key = $"{ItemPrefix}:{enrollmentId}";
        if (_cache.TryGetValue(key, out EnrollmentResponse? cached) && cached is not null) return cached;
        var e = await _enrollments.GetByIdAsync(enrollmentId);
        if (e is null) return null;
        var r = Map(e);
        _cache.Set(key, r, CacheDuration);
        return r;
    }

    public async Task<PagedResult<EnrollmentResponse>> ListAsync(long? patientId, string? status, int page, int pageSize)
    {
        var v = GetVersion(VersionKey);
        var key = $"enrollments:list:v{v}:{page}:{pageSize}:{patientId}:{status}";
        if (_cache.TryGetValue(key, out PagedResult<EnrollmentResponse>? cached) && cached is not null) return cached;
        var (items, total) = await _enrollments.ListAsync(patientId, status, page, pageSize);
        var result = new PagedResult<EnrollmentResponse> { Page = page, PageSize = pageSize, TotalCount = total, Items = items.Select(Map).ToList() };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<EnrollmentResponse> CreateAsync(CreateEnrollmentRequest req)
    {
        if (await _patients.GetByIdAsync(req.PatientID) is null)
            throw new DomainException($"Patient {req.PatientID} not found.");
        var enrollment = new Enrollment { PatientID = req.PatientID, SiteProtocolID = req.SiteProtocolID, EnrollmentDate = req.EnrollmentDate, ConsentDate = req.ConsentDate, Status = req.Status, WithdrawalReason = null };
        await _enrollments.AddAsync(enrollment);
        BumpVersion(VersionKey);
        // Keep Patients.EnrollmentStatus in sync with the new enrollment
        await SyncPatientStatusAsync(req.PatientID);
        return Map(enrollment);
    }

    public async Task<EnrollmentResponse?> UpdateAsync(long enrollmentId, UpdateEnrollmentRequest req)
    {
        var enrollment = await _enrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null) return null;
        if (enrollment.Status == "Withdrawn" && req.Status != "Withdrawn")
            throw new DomainException("A withdrawn enrollment cannot be reactivated.");
        enrollment.ConsentDate = req.ConsentDate; enrollment.Status = req.Status; enrollment.WithdrawalReason = req.WithdrawalReason;
        await _enrollments.UpdateAsync(enrollment);
        Invalidate(ItemPrefix, enrollmentId, VersionKey);
        // Keep Patients.EnrollmentStatus in sync with the updated status
        await SyncPatientStatusAsync(enrollment.PatientID);
        return Map(enrollment);
    }

    // ── Patient status sync ──────────────────────────────────────────────────

    /// <summary>
    /// Derives the correct Patient.EnrollmentStatus from ALL current enrollments for that
    /// patient, then writes it with a targeted SQL UPDATE and invalidates the patient cache.
    /// Priority: Active &gt; Screening &gt; Completed &gt; Withdrawn.
    /// </summary>
    private async Task SyncPatientStatusAsync(long patientId)
    {
        var (allEnrollments, _) = await _enrollments.ListAsync(patientId, null, 1, 200);
        var derivedStatus = DerivePatientStatus(allEnrollments);
        await _patients.UpdateEnrollmentStatusAsync(patientId, derivedStatus);
        // Invalidate both the per-item and list-level patient caches
        _cache.Remove($"patients:item:{patientId}");
        BumpVersion("patients:list:version");
    }

    /// <summary>
    /// Active &gt; Screening &gt; Completed &gt; Withdrawn.
    /// Returns "Screening" when there are no enrollments yet.
    /// </summary>
    private static string DerivePatientStatus(IEnumerable<Enrollment> enrollments)
    {
        var statuses = enrollments.Select(e => e.Status)
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (statuses.Count == 0) return "Screening";
        if (statuses.Contains("Active")) return "Active";
        if (statuses.Contains("Screening")) return "Screening";
        if (statuses.Contains("Completed")) return "Completed";
        return "Withdrawn";
    }

    private int GetVersion(string key) => _cache.GetOrCreate(key, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });
    private void BumpVersion(string key) => _cache.Set(key, GetVersion(key) + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    private void Invalidate(string itemPrefix, long id, string versionKey) { _cache.Remove($"{itemPrefix}:{id}"); BumpVersion(versionKey); }

    private static EnrollmentResponse Map(Enrollment e) => new(e.EnrollmentID, e.PatientID, e.SiteProtocolID, e.EnrollmentDate, e.ConsentDate, e.Status, e.WithdrawalReason);
}
