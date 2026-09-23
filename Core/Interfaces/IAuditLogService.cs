using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string details, Guid? userId = null, string? ipAddress = null, CancellationToken cancellationToken = default);
    Task LogActionAsync(Guid? userId, string action, string details, string? ipAddress = null, CancellationToken cancellationToken = default);
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);
}
