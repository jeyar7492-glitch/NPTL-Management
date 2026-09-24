using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface ICertificateRepository
{
    Task<Certificate?> GetByIdAsync(Guid certificateId, CancellationToken cancellationToken = default);
    Task<Certificate?> GetByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
