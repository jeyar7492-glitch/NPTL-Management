using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminRepository
{
    Task<Admin?> GetByIdAsync(Guid adminId, CancellationToken cancellationToken = default);
    Task<Admin?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Admin?> GetByIdentifierAsync(string adminIdentifier, CancellationToken cancellationToken = default);
}
