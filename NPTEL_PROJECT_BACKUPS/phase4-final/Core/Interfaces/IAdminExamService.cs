using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminExamService
{
    Task<PagedResult<AdminExamDto>> GetExamsAsync(string? search, string? examStatus, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminExamDto?> GetExamByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<AdminExamDto> UpdateExamAsync(Guid registrationId, UpdateAdminExamDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
