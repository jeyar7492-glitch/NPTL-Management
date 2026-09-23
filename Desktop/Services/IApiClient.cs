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
    Task<StaffProfileResponse> GetStaffProfileAsync(CancellationToken cancellationToken = default);
    Task<List<StudentProfileResponse>> GetAssignedStudentsAsync(string? search = null, string? classSection = null, CancellationToken cancellationToken = default);
    Task<StudentProfileResponse> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminProfileResponse> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
}
