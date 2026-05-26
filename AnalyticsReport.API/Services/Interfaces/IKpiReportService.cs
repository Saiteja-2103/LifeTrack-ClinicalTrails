using AnalyticsReport.API.DTOs;
using Shared.CL.DTOs;

namespace AnalyticsReport.API.Services.Interfaces;

public interface IKpiReportService
{
    Task<PagedResult<KpiReportResponse>> ListAsync(string? scope, int page, int pageSize);
    Task<KpiReportResponse?> GetAsync(long id);
    Task<KpiReportResponse> CreateAsync(CreateKpiReportRequest req);
}
