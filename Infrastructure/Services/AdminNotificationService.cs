using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminNotificationService(ApplicationDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminNotificationDto>> GetNotificationsAsync(
        string? search, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .Include(n => n.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(n =>
                n.Title.ToLower().Contains(s) ||
                n.Message.ToLower().Contains(s) ||
                (n.User != null && n.User.Username.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new AdminNotificationDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Username = n.User != null ? n.User.Username : string.Empty,
                RecipientName = n.User != null ? n.User.Username : string.Empty,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                RelatedRegistrationId = n.RelatedRegistrationId
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminNotificationDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> CreateNotificationAsync(
        CreateNotificationDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Notification title cannot be empty.");
        if (string.IsNullOrWhiteSpace(dto.Message))
            throw new ArgumentException("Notification message cannot be empty.");

        var targetUserIds = new HashSet<Guid>();

        switch (dto.TargetType?.Trim())
        {
            case "Individual":
                if (!dto.TargetUserId.HasValue || dto.TargetUserId.Value == Guid.Empty)
                    throw new ArgumentException("Target user ID is required for Individual notification.");
                targetUserIds.Add(dto.TargetUserId.Value);
                break;

            case "Year":
                if (!dto.Year.HasValue)
                    throw new ArgumentException("Target year is required for Year group notification.");
                var yearStudents = await _context.Students
                    .Where(s => s.Year == dto.Year.Value &&
                               (string.IsNullOrEmpty(dto.Department) || s.Department == dto.Department))
                    .Select(s => s.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var id in yearStudents) targetUserIds.Add(id);
                break;

            case "Class":
                if (!dto.Year.HasValue || string.IsNullOrWhiteSpace(dto.ClassSection))
                    throw new ArgumentException("Target year and class section are required for Class group notification.");
                var classStudents = await _context.Students
                    .Where(s => s.Year == dto.Year.Value &&
                                s.ClassSection == dto.ClassSection &&
                                (string.IsNullOrEmpty(dto.Department) || s.Department == dto.Department))
                    .Select(s => s.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var id in classStudents) targetUserIds.Add(id);
                break;

            case "AllDepartment":
            default:
                var dept = string.IsNullOrWhiteSpace(dto.Department) ? "CSE" : dto.Department;
                var deptStudents = await _context.Students
                    .Where(s => s.Department == dept)
                    .Select(s => s.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var id in deptStudents) targetUserIds.Add(id);
                break;
        }

        if (targetUserIds.Count == 0)
        {
            return 0;
        }

        var notifications = targetUserIds.Select(uid => new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = uid,
            Title = dto.Title.Trim(),
            Message = dto.Message.Trim(),
            IsRead = false,
            RelatedRegistrationId = dto.RelatedRegistrationId,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "NotificationsDispatched",
            $"Dispatched notification '{dto.Title}' to {notifications.Count} recipient(s) [Target: {dto.TargetType}]",
            ipAddress,
            cancellationToken);

        return notifications.Count;
    }

    public async Task<bool> DeleteNotificationAsync(
        Guid notificationId, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.NotificationId == notificationId, cancellationToken);
        if (n == null) return false;

        _context.Notifications.Remove(n);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "NotificationDeleted",
            $"Deleted notification ID {notificationId} with title '{n.Title}'",
            ipAddress,
            cancellationToken);

        return true;
    }

    public async Task<int> TriggerAutomatedRulesAsync(CancellationToken cancellationToken = default)
    {
        int generatedCount = 0;
        var now = DateTime.UtcNow;

        // Rule 0: Student-scheduled certificate reminder
        var dueCertificateReminders = await _context.Certificates
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Student)
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Course)
            .Where(c => c.ReminderEnabled &&
                        c.ReminderDate.HasValue &&
                        c.ReminderDate.Value <= now &&
                        c.LastReminderSentDate == null &&
                        c.VerifiedStatus != CertificateStatus.Verified &&
                        c.VerifiedStatus != CertificateStatus.Received)
            .ToListAsync(cancellationToken);

        foreach (var certificate in dueCertificateReminders)
        {
            if (certificate.Registration?.Student == null) continue;

            _context.Notifications.Add(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = certificate.Registration.Student.UserId,
                Title = "Certificate Submission Reminder",
                Message = $"Please upload or update your NPTEL certificate for '{certificate.Registration.Course?.CourseName}'. Your certificate reminder is due.",
                IsRead = false,
                RelatedRegistrationId = certificate.RegistrationId,
                CreatedAt = now
            });

            certificate.LastReminderSentDate = now;
            certificate.ReminderEnabled = false;
            certificate.ReminderDate = null;
            certificate.UpdatedAt = now;
            generatedCount++;
        }

        // Rule 1: Exam Application Deadline within 7 days
        var upcomingDeadlineLimit = now.AddDays(7);
        var pendingExamApps = await _context.ExamStatuses
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Student)
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Course)
            .Where(e => e.ExamApplicationDeadline.HasValue &&
                        e.ExamApplicationDeadline.Value >= now &&
                        e.ExamApplicationDeadline.Value <= upcomingDeadlineLimit &&
                        (e.ExamApplicationStatus == "NotStarted" || e.ExamApplicationStatus == "Pending"))
            .ToListAsync(cancellationToken);

        foreach (var item in pendingExamApps)
        {
            if (item.Registration?.Student == null) continue;

            // Check if notification already sent in the last 3 days
            var recentNotif = await _context.Notifications
                .AnyAsync(n => n.UserId == item.Registration.Student.UserId &&
                               n.Title.Contains("Exam Application Deadline") &&
                               n.CreatedAt >= now.AddDays(-3), cancellationToken);

            if (!recentNotif)
            {
                var daysLeft = (int)Math.Ceiling((item.ExamApplicationDeadline!.Value - now).TotalDays);
                _context.Notifications.Add(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = item.Registration.Student.UserId,
                    Title = $"Reminder: NPTEL Exam Application Deadline in {daysLeft} day(s)",
                    Message = $"The examination registration deadline for course '{item.Registration.Course?.CourseName}' is approaching on {item.ExamApplicationDeadline.Value:MMM dd, yyyy}. Please register and pay the exam fee.",
                    IsRead = false,
                    RelatedRegistrationId = item.RegistrationId,
                    CreatedAt = DateTime.UtcNow
                });
                generatedCount++;
            }
        }

        // Rule 2: Proctored Exam Date within 3 days
        var examDateLimit = now.AddDays(3);
        var scheduledExams = await _context.ExamStatuses
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Student)
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Course)
            .Where(e => e.ExamDate.HasValue &&
                        e.ExamDate.Value >= now &&
                        e.ExamDate.Value <= examDateLimit &&
                        (e.Status == "Scheduled" || e.ExamApplicationStatus == "Applied"))
            .ToListAsync(cancellationToken);

        foreach (var item in scheduledExams)
        {
            if (item.Registration?.Student == null) continue;

            var recentNotif = await _context.Notifications
                .AnyAsync(n => n.UserId == item.Registration.Student.UserId &&
                               n.Title.Contains("Upcoming NPTEL Exam") &&
                               n.CreatedAt >= now.AddDays(-2), cancellationToken);

            if (!recentNotif)
            {
                _context.Notifications.Add(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = item.Registration.Student.UserId,
                    Title = "Upcoming NPTEL Exam Alert",
                    Message = $"Your proctored exam for '{item.Registration.Course?.CourseName}' is scheduled on {item.ExamDate!.Value:MMM dd, yyyy}. Please download your hall ticket and carry college ID to the exam center.",
                    IsRead = false,
                    RelatedRegistrationId = item.RegistrationId,
                    CreatedAt = DateTime.UtcNow
                });
                generatedCount++;
            }
        }

        if (generatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogService.LogActionAsync(
                null,
                "AutomatedNotificationEngine",
                $"Triggered automated rules engine: generated {generatedCount} reminder notifications.",
                "SystemWorker",
                cancellationToken);
        }

        return generatedCount;
    }
}
