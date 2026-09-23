using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IStudentCourseService
{
    Task<StudentDashboardSummaryDto> GetDashboardSummaryAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<StudentCourseDto>> GetStudentCoursesAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<StudentCourseDetailsDto?> GetCourseDetailsAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default);
    Task<List<StudentTimelineItemDto>?> GetCourseTimelineAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default);
    Task<StudentExamDto?> GetCourseExamStatusAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default);
    Task<StudentCertificateDto?> GetCourseCertificateStatusAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default);
}
