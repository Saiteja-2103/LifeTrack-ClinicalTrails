using AdverseEvent.API.DTOs;
using Shared.CL.DTOs;

namespace AdverseEvent.API.Services.Interfaces;

public interface IAdverseEventService
{
    Task<PagedResult<AdverseEventResponse>> ListAsync(
        long? protocolId, long? patientId, string? severity, string? status, int page, int pageSize);
    Task<AdverseEventResponse?> GetAsync(long id);
    Task<AdverseEventResponse> CreateAsync(CreateAdverseEventRequest req);
    Task<AdverseEventResponse?> UpdateAsync(long id, UpdateAdverseEventRequest req);
}
