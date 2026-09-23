using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class StudentNotificationService : IStudentNotificationService
{
    private readonly ApplicationDbContext _context;

    public StudentNotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StudentNotificationDto>> GetStudentNotificationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Enforce user ownership: only fetch notifications where UserId matches authenticated user
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new StudentNotificationDto
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                RelatedRegistrationId = n.RelatedRegistrationId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkNotificationAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        // Enforce ownership: only mark read if the notification belongs to authenticated user
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId, cancellationToken);

        if (notification == null)
        {
            return false;
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
