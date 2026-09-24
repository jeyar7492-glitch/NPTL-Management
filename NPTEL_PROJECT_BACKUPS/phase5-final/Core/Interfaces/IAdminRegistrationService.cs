using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminRegistrationService
{
    Task<PagedResult<AdminRegistrationDto>> GetRegistrationsAsync(string? search, string? status, Guid? studentId, Guid? courseId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminRegistrationDto?> GetRegistrationByIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<AdminRegistrationDto> CreateRegistrationAsync(CreateRegistrationDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AdminRegistrationDto> UpdateRegistrationStatusAsync(Guid registrationId, UpdateRegistrationDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
