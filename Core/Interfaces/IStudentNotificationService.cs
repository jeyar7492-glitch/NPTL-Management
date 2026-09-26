using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IStudentNotificationService
{
    Task<List<StudentNotificationDto>> GetStudentNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<StudentCertificateReminderResponseDto?> SetCertificateReminderAsync(Guid userId, Guid registrationId, StudentCertificateReminderDto request, CancellationToken cancellationToken = default);
}
