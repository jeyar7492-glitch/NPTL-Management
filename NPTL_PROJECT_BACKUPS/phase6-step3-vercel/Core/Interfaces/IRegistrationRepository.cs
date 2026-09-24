using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface IRegistrationRepository
{
    Task<NptelRegistration?> GetByIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NptelRegistration>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
