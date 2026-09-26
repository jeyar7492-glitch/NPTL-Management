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

    public async Task<StudentCertificateReminderResponseDto?> SetCertificateReminderAsync(
        Guid userId,
        Guid registrationId,
        StudentCertificateReminderDto request,
        CancellationToken cancellationToken = default)
    {
        var registration = await _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(
                r => r.RegistrationId == registrationId &&
                     r.Student != null &&
                     r.Student.UserId == userId,
                cancellationToken);

        if (registration == null)
            return null;

        if (registration.Certificate == null)
        {
            registration.Certificate = new NPTELManagement.Core.Entities.Certificate
            {
                CertificateId = Guid.NewGuid(),
                RegistrationId = registrationId,
                VerifiedStatus = NPTELManagement.Core.Enums.CertificateStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Certificates.Add(registration.Certificate);
        }

        if (request.Enabled)
        {
            if (!request.ReminderDate.HasValue)
                throw new ArgumentException("Please choose a reminder date.");

            var reminderDate = request.ReminderDate.Value;
            if (reminderDate < DateTime.UtcNow.AddMinutes(-1))
                throw new ArgumentException("Reminder date must be in the future.");

            if (reminderDate > DateTime.UtcNow.AddDays(90))
                throw new ArgumentException("Reminder date cannot be more than 90 days ahead.");

            registration.Certificate.ReminderEnabled = true;
            registration.Certificate.ReminderDate = reminderDate.ToUniversalTime();
            registration.Certificate.LastReminderSentDate = null;
        }
        else
        {
            registration.Certificate.ReminderEnabled = false;
            registration.Certificate.ReminderDate = null;
            registration.Certificate.LastReminderSentDate = null;
        }

        registration.Certificate.UpdatedAt = DateTime.UtcNow;

        _context.Notifications.Add(new NPTELManagement.Core.Entities.Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = userId,
            Title = request.Enabled ? "Certificate Reminder Enabled" : "Certificate Reminder Disabled",
            Message = request.Enabled
                ? $"Certificate reminder for '{registration.Course?.CourseName}' is set for {registration.Certificate.ReminderDate!.Value:dd MMM yyyy HH:mm} UTC."
                : $"Certificate reminder for '{registration.Course?.CourseName}' has been disabled.",
            IsRead = false,
            RelatedRegistrationId = registrationId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new StudentCertificateReminderResponseDto
        {
            RegistrationId = registrationId,
            Enabled = registration.Certificate.ReminderEnabled,
            ReminderDate = registration.Certificate.ReminderDate,
            Message = request.Enabled
                ? "Certificate reminder enabled."
                : "Certificate reminder disabled."
        };
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
