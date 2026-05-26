using Patient.API.DTOs;
using Shared.CL.DTOs;
namespace Patient.API.Services.Interfaces;

public interface IVisitService
{
    Task<PagedResult<VisitResponse>> ListAsync(string? status, long? enrollmentId, int page, int pageSize);
    Task<VisitResponse?> GetAsync(long visitId);
    Task<VisitResponse> CreateAsync(CreateVisitRequest req);
    Task<VisitResponse?> UpdateAsync(long visitId, UpdateVisitRequest req);
}
