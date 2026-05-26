using Microsoft.Extensions.Caching.Memory;
using Patient.API.DTOs;
using Patient.API.Models;
using Shared.CL.DTOs;
using Patient.API.Repositories.Interfaces;
using Patient.API.Services.Interfaces;
namespace Patient.API.Services;

public class PatientService : IPatientService
{
    private readonly IPatientRepository _patients;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "patients:list:version";
    private const string ItemPrefix = "patients:item";

    public PatientService(IPatientRepository patients, IMemoryCache cache)
    { _patients = patients; _cache = cache; }

    public async Task<PagedResult<PatientResponse>> ListAsync(string? enrollmentStatus, int page, int pageSize)
    {
        var v = GetVersion(VersionKey);
        var key = $"patients:list:v{v}:{page}:{pageSize}:{enrollmentStatus}";
        if (_cache.TryGetValue(key, out PagedResult<PatientResponse>? cached) && cached is not null) return cached;
        var (items, total) = await _patients.ListAsync(enrollmentStatus, page, pageSize);
        var result = new PagedResult<PatientResponse> { Page = page, PageSize = pageSize, TotalCount = total, Items = items.Select(ToResponse).ToList() };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<PatientResponse?> GetAsync(long patientId)
    {
        var key = $"{ItemPrefix}:{patientId}";
        if (_cache.TryGetValue(key, out PatientResponse? cached) && cached is not null) return cached;
        var p = await _patients.GetByIdAsync(patientId);
        if (p is null) return null;
        var r = ToResponse(p);
        _cache.Set(key, r, CacheDuration);
        return r;
    }

    public async Task<PatientResponse> CreateAsync(CreatePatientRequest req)
    {
        var patient = new PatientRecord
        {
            UserID = req.UserID,
            Name = req.Name.Trim(),
            DOB = req.DOB,
            ContactInfo = req.ContactInfo ?? string.Empty,
            EnrollmentStatus = req.EnrollmentStatus
        };
        var created = await _patients.AddAsync(patient);
        BumpVersion(VersionKey);
        return ToResponse(created);
    }

    public async Task<PatientResponse?> UpdateAsync(long patientId, UpdatePatientRequest req)
    {
        var patient = await _patients.GetByIdAsync(patientId);
        if (patient is null) return null;
        patient.Name = req.Name.Trim(); patient.DOB = req.DOB; patient.ContactInfo = req.ContactInfo ?? string.Empty; patient.EnrollmentStatus = req.EnrollmentStatus;
        await _patients.UpdateAsync(patient);
        Invalidate(ItemPrefix, patientId, VersionKey);
        return ToResponse(patient);
    }

    private int GetVersion(string key) => _cache.GetOrCreate(key, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });
    private void BumpVersion(string key) => _cache.Set(key, GetVersion(key) + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    private void Invalidate(string itemPrefix, long id, string versionKey) { _cache.Remove($"{itemPrefix}:{id}"); BumpVersion(versionKey); }

    private static PatientResponse ToResponse(PatientRecord p) =>
        new(p.PatientID, p.UserID, p.Name, p.DOB, p.ContactInfo, p.EnrollmentStatus);
}
