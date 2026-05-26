using Patient.API.DTOs;
using Shared.CL.DTOs;
namespace Patient.API.Services.Interfaces;

public interface IPatientService
{
    Task<PagedResult<PatientResponse>> ListAsync(string? enrollmentStatus, int page, int pageSize);
    Task<PatientResponse?> GetAsync(long patientId);
    Task<PatientResponse> CreateAsync(CreatePatientRequest req);
    Task<PatientResponse?> UpdateAsync(long patientId, UpdatePatientRequest req);
}
