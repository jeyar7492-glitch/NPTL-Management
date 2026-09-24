using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminNotificationService
{
    Task<PagedResult<AdminNotificationDto>> GetNotificationsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CreateNotificationAsync(CreateNotificationDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> DeleteNotificationAsync(Guid notificationId, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<int> TriggerAutomatedRulesAsync(CancellationToken cancellationToken = default);
}
