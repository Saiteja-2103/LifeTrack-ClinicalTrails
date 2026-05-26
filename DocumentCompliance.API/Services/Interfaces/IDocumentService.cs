using DocumentCompliance.API.DTOs;
using Shared.CL.DTOs;

namespace DocumentCompliance.API.Services.Interfaces;

public interface IDocumentService
{
    Task<PagedResult<DocumentResponse>> ListAsync(
        long? protocolId, string? status, string? type, int page, int pageSize);
    Task<DocumentResponse?> GetAsync(long documentId);
    Task<DocumentResponse> CreateAsync(CreateDocumentRequest req);
    Task<DocumentResponse?> UpdateAsync(long documentId, UpdateDocumentRequest req);
}
