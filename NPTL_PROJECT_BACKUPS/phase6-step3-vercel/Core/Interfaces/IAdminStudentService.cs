using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminStudentService
{
    Task<PagedResult<AdminStudentListDto>> GetStudentsAsync(string? search, string? department, int? year, string? classSection, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminStudentDetailDto?> GetStudentByIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<AdminStudentDetailDto> CreateStudentAsync(CreateStudentDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AdminStudentDetailDto> UpdateStudentAsync(Guid studentId, UpdateStudentDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> UpdateStudentStatusAsync(Guid studentId, bool isActive, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
