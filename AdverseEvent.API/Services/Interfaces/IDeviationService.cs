using AdverseEvent.API.DTOs;
using Shared.CL.DTOs;

namespace AdverseEvent.API.Services.Interfaces;

public interface IDeviationService
{
    Task<PagedResult<DeviationResponse>> ListAsync(
        long? siteProtocolId, string? severity, string? status, int page, int pageSize);
    Task<DeviationResponse?> GetAsync(long id);
    Task<DeviationResponse> CreateAsync(CreateDeviationRequest req);
    Task<DeviationResponse?> UpdateAsync(long id, UpdateDeviationRequest req);
}
