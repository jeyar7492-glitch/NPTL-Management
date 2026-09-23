using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Models;

namespace NPTELManagement.Desktop.Services;

public interface IApiClient
{
    Task<HealthCheckResult> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<AuthSuccessResponse> LoginStudentAsync(StudentLoginDto dto, CancellationToken cancellationToken = default);
    Task<AuthSuccessResponse> LoginStaffAsync(StaffLoginDto dto, CancellationToken cancellationToken = default);
    Task<AuthSuccessResponse> LoginAdminAsync(AdminLoginDto dto, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<StudentProfileResponse> GetStudentProfileAsync(CancellationToken cancellationToken = default);
    Task<StudentDashboardSummaryDto> GetStudentDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task<List<StudentCourseDto>> GetStudentCoursesAsync(CancellationToken cancellationToken = default);
    Task<StudentCourseDetailsDto> GetStudentCourseDetailsAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<List<StudentNotificationDto>> GetStudentNotificationsAsync(CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<StaffProfileResponse> GetStaffProfileAsync(CancellationToken cancellationToken = default);
    Task<StaffDashboardSummaryDto> GetStaffDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<StaffStudentListDto>> GetStaffStudentsAsync(StaffStudentFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<StudentProfileResponse>> GetAssignedStudentsAsync(string? search = null, string? classSection = null, CancellationToken cancellationToken = default);
    Task<StaffStudentDetailsDto> GetStaffStudentDetailsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<StaffStudentCourseDto>> GetStaffStudentCoursesAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<StudentTimelineItemDto>> GetStaffStudentTimelineAsync(Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default);
    Task<StudentExamDto> GetStaffStudentExamAsync(Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default);
    Task<StudentCertificateDto> GetStaffStudentCertificateAsync(Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default);
    Task<StaffReportPreviewDto> GetStaffReportPreviewAsync(string reportType, CancellationToken cancellationToken = default);
    Task<StudentProfileResponse> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminProfileResponse> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
}
