using ProtocolSite.API.DTOs;
using Shared.CL.DTOs;
namespace ProtocolSite.API.Services.Interfaces;

public interface ISiteProtocolService
{
    Task<SiteProtocolResponse?> GetAsync(long id);
    Task<PagedResult<SiteProtocolResponse>> ListAsync(long? siteId, long? protocolId, long? investigatorId, string? status, int page, int pageSize);
    Task<SiteProtocolResponse> CreateAsync(CreateSiteProtocolRequest req);
    Task<SiteProtocolResponse?> UpdateAsync(long id, UpdateSiteProtocolRequest req);
}
