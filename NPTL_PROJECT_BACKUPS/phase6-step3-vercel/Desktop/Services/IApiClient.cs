using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Models;

namespace NPTELManagement.Desktop.Services;

public interface IApiClient
{
    Task<HealthCheckResult> GetHealthAsync(CancellationToken cancellationToken = default);

    // Auth
    Task<AuthSuccessResponse> LoginStudentAsync(StudentLoginDto dto, CancellationToken cancellationToken = default);
    Task<AuthSuccessResponse> LoginStaffAsync(StaffLoginDto dto, CancellationToken cancellationToken = default);
    Task<AuthSuccessResponse> LoginAdminAsync(AdminLoginDto dto, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);

    // Student Module
    Task<StudentProfileResponse> GetStudentProfileAsync(CancellationToken cancellationToken = default);
    Task<StudentDashboardSummaryDto> GetStudentDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task<List<StudentCourseDto>> GetStudentCoursesAsync(CancellationToken cancellationToken = default);
    Task<StudentCourseDetailsDto> GetStudentCourseDetailsAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<List<StudentNotificationDto>> GetStudentNotificationsAsync(CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<StudentProfileResponse> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Staff Module
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

    // Phase 4 — Admin Module
    Task<AdminProfileResponse> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync(CancellationToken cancellationToken = default);

    // Admin Students
    Task<PagedResult<AdminStudentListDto>> GetAdminStudentsAsync(string? search, string? department, int? year, string? classSection, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminStudentDetailDto> GetAdminStudentByIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<AdminStudentDetailDto> CreateAdminStudentAsync(CreateStudentDto dto, CancellationToken cancellationToken = default);
    Task<AdminStudentDetailDto> UpdateAdminStudentAsync(Guid studentId, UpdateStudentDto dto, CancellationToken cancellationToken = default);
    Task<bool> UpdateAdminStudentStatusAsync(Guid studentId, bool isActive, CancellationToken cancellationToken = default);

    // Admin Staff
    Task<PagedResult<AdminStaffListDto>> GetAdminStaffAsync(string? search, string? department, int? assignedYear, string? assignedClass, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminStaffDetailDto> GetAdminStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<AdminStaffDetailDto> CreateAdminStaffAsync(CreateStaffDto dto, CancellationToken cancellationToken = default);
    Task<AdminStaffDetailDto> UpdateAdminStaffAsync(Guid staffId, UpdateStaffDto dto, CancellationToken cancellationToken = default);
    Task<bool> UpdateAdminStaffStatusAsync(Guid staffId, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> ResetAdminStaffPasswordAsync(Guid staffId, string newPassword, CancellationToken cancellationToken = default);

    // Admin Courses
    Task<PagedResult<AdminCourseDto>> GetAdminCoursesAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCourseDto> GetAdminCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<AdminCourseDto> CreateAdminCourseAsync(CreateCourseDto dto, CancellationToken cancellationToken = default);
    Task<AdminCourseDto> UpdateAdminCourseAsync(Guid courseId, UpdateCourseDto dto, CancellationToken cancellationToken = default);
    Task<bool> UpdateAdminCourseStatusAsync(Guid courseId, string status, CancellationToken cancellationToken = default);

    // Admin Registrations
    Task<PagedResult<AdminRegistrationDto>> GetAdminRegistrationsAsync(string? search, string? status, Guid? studentId, Guid? courseId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminRegistrationDto> GetAdminRegistrationByIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<AdminRegistrationDto> CreateAdminRegistrationAsync(CreateRegistrationDto dto, CancellationToken cancellationToken = default);
    Task<AdminRegistrationDto> UpdateAdminRegistrationStatusAsync(Guid registrationId, string status, CancellationToken cancellationToken = default);

    // Admin Exams
    Task<PagedResult<AdminExamDto>> GetAdminExamsAsync(string? search, string? examStatus, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminExamDto> GetAdminExamByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<AdminExamDto> UpdateAdminExamAsync(Guid registrationId, UpdateAdminExamDto dto, CancellationToken cancellationToken = default);

    // Admin Certificates & Cloud Access
    Task<PagedResult<AdminCertificateDto>> GetAdminCertificatesAsync(string? search, string? verifiedStatus, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCertificateDto> GetAdminCertificateByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<AdminCertificateDto> UploadAdminCertificateAsync(Guid registrationId, string filePath, CancellationToken cancellationToken = default);
    Task<AdminCertificateDto> UpdateAdminCertificateStatusAsync(Guid registrationId, string verifiedStatus, DateTime? verifiedDate = null, CancellationToken cancellationToken = default);
    Task<CertificateAccessResponseDto> GetCertificateAccessAsync(Guid certificateId, CancellationToken cancellationToken = default);

    // Admin Notifications
    Task<PagedResult<AdminNotificationDto>> GetAdminNotificationsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CreateAdminNotificationAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAdminNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<int> TriggerAdminNotificationRulesAsync(CancellationToken cancellationToken = default);

    // Admin Reports & Exports
    Task<AdminReportPreviewDto> GetAdminReportPreviewAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAdminReportPdfAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAdminReportXlsxAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAdminReportCsvAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);

    // Admin Audit Logs
    Task<PagedResult<AuditLogDto>> GetAdminAuditLogsAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);
}
