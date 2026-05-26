namespace Shared.CL.DTOs;

public class AuditEntryDto
{
    public long AuditEntryID { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string? PrimaryKey { get; set; }
    public string Action { get; set; } = string.Empty;
    public long? ChangedByUserID { get; set; }
    public DateTime ChangedAt { get; set; }
}
