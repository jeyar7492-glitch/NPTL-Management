using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Student?> GetByRegisterNumberAsync(string registerNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetAssignedStudentsAsync(string department, int year, string? classSection, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
