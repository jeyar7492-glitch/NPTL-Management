using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminStaffService
{
    Task<PagedResult<AdminStaffListDto>> GetStaffMembersAsync(string? search, string? department, int? assignedYear, string? assignedClass, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminStaffDetailDto?> GetStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<AdminStaffDetailDto> CreateStaffAsync(CreateStaffDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AdminStaffDetailDto> UpdateStaffAsync(Guid staffId, UpdateStaffDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> UpdateStaffStatusAsync(Guid staffId, bool isActive, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> ResetStaffPasswordAsync(Guid staffId, ResetStaffPasswordDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
