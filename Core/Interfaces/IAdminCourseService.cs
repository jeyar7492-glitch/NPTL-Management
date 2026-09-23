using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminCourseService
{
    Task<PagedResult<AdminCourseDto>> GetCoursesAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<AdminCourseDto> CreateCourseAsync(CreateCourseDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AdminCourseDto> UpdateCourseAsync(Guid courseId, UpdateCourseDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> UpdateCourseStatusAsync(Guid courseId, string status, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
