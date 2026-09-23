using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> AddAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);
}
