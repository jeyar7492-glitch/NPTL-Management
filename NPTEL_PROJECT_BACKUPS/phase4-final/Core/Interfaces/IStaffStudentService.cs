using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IStaffStudentService
{
    Task<StaffProfileResponse?> GetStaffProfileAsync(Guid staffUserId, CancellationToken cancellationToken = default);

    Task<StaffDashboardSummaryDto> GetDashboardSummaryAsync(Guid staffUserId, CancellationToken cancellationToken = default);

    Task<PagedResult<StaffStudentListDto>> GetAssignedStudentsAsync(
        Guid staffUserId, 
        StaffStudentFilterDto filter, 
        CancellationToken cancellationToken = default);

    Task<StaffStudentDetailsDto?> GetStudentDetailsAsync(Guid staffUserId, Guid studentId, CancellationToken cancellationToken = default);

    Task<List<StaffStudentCourseDto>?> GetStudentCoursesAsync(Guid staffUserId, Guid studentId, CancellationToken cancellationToken = default);

    Task<List<StudentTimelineItemDto>?> GetStudentTimelineAsync(Guid staffUserId, Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default);

    Task<StudentExamDto?> GetStudentExamStatusAsync(Guid staffUserId, Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default);

    Task<StudentCertificateDto?> GetStudentCertificateStatusAsync(Guid staffUserId, Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default);
}
