using Patient.API.DTOs;
using Shared.CL.DTOs;
namespace Patient.API.Services.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponse?> GetAsync(long enrollmentId);
    Task<PagedResult<EnrollmentResponse>> ListAsync(long? patientId, string? status, int page, int pageSize);
    Task<EnrollmentResponse> CreateAsync(CreateEnrollmentRequest req);
    Task<EnrollmentResponse?> UpdateAsync(long enrollmentId, UpdateEnrollmentRequest req);
}
