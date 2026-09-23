using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<Course?> GetByCodeAsync(string courseCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
