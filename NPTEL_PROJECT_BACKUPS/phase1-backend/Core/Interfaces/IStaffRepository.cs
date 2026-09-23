using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<Staff?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Staff?> GetByIdentifierAsync(string staffIdentifier, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
