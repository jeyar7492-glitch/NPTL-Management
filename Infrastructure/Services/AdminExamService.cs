using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AdminExamService : IAdminExamService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminExamService(ApplicationDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminExamDto>> GetExamsAsync(
        string? search, 
        string? examStatus, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.ExamStatuses
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Student)
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Course)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(examStatus))
        {
            var st = examStatus.Trim().ToLower();
            query = query.Where(e => (e.Status != null && e.Status.ToLower() == st) ||
                                     (e.ExamApplicationStatus != null && e.ExamApplicationStatus.ToLower() == st));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e =>
                (e.Registration != null && e.Registration.Student != null &&
                    (e.Registration.Student.Name.ToLower().Contains(s) || e.Registration.Student.RegisterNumber.ToLower().Contains(s))) ||
                (e.Registration != null && e.Registration.Course != null &&
                    (e.Registration.Course.CourseName.ToLower().Contains(s) || e.Registration.Course.CourseCode.ToLower().Contains(s))));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AdminExamDto
            {
                ExamStatusId = e.ExamStatusId,
                RegistrationId = e.RegistrationId,
                StudentName = e.Registration != null && e.Registration.Student != null ? e.Registration.Student.Name : string.Empty,
                RegisterNumber = e.Registration != null && e.Registration.Student != null ? e.Registration.Student.RegisterNumber : string.Empty,
                CourseName = e.Registration != null && e.Registration.Course != null ? e.Registration.Course.CourseName : string.Empty,
                ExamApplicationStatus = e.ExamApplicationStatus ?? "NotStarted",
                ExamApplicationDate = e.ExamApplicationDate,
                ExamApplicationDeadline = e.ExamApplicationDeadline,
                ExamDate = e.ExamDate,
                LastResultReminderDate = e.LastResultReminderDate,
                HallTicketStatus = e.HallTicketStatus,
                ExamStatus = e.Status ?? "NotStarted",
                Score = e.Score,
                PassStatus = e.PassStatus
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminExamDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminExamDto?> GetExamByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var e = await _context.ExamStatuses
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Student)
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Course)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.RegistrationId == registrationId, cancellationToken);

        if (e == null) return null;

        return new AdminExamDto
        {
            ExamStatusId = e.ExamStatusId,
            RegistrationId = e.RegistrationId,
            StudentName = e.Registration?.Student?.Name ?? string.Empty,
            RegisterNumber = e.Registration?.Student?.RegisterNumber ?? string.Empty,
            CourseName = e.Registration?.Course?.CourseName ?? string.Empty,
            ExamApplicationStatus = e.ExamApplicationStatus ?? "NotStarted",
            ExamApplicationDate = e.ExamApplicationDate,
            ExamApplicationDeadline = e.ExamApplicationDeadline,
            ExamDate = e.ExamDate,
            LastResultReminderDate = e.LastResultReminderDate,
            HallTicketStatus = e.HallTicketStatus,
            ExamStatus = e.Status ?? "NotStarted",
            Score = e.Score,
            PassStatus = e.PassStatus
        };
    }

    public async Task<AdminExamDto> UpdateExamAsync(
        Guid registrationId, 
        UpdateAdminExamDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var exam = await _context.ExamStatuses
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Student)
            .Include(e => e.Registration)
                .ThenInclude(r => r!.Course)
            .FirstOrDefaultAsync(e => e.RegistrationId == registrationId, cancellationToken);

        if (exam == null)
        {
            // If ExamStatus doesn't exist yet, create one for the registration
            var reg = await _context.NptelRegistrations
                .Include(r => r.Student)
                .Include(r => r.Course)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);

            if (reg == null)
                throw new KeyNotFoundException($"Registration with ID {registrationId} not found.");

            exam = new ExamStatus
            {
                ExamStatusId = Guid.NewGuid(),
                RegistrationId = registrationId,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ExamStatuses.Add(exam);
            await _context.SaveChangesAsync(cancellationToken);

            exam.Registration = reg;
        }

        if (dto.Score.HasValue && (dto.Score.Value < 0 || dto.Score.Value > 100))
        {
            throw new ArgumentException("Exam score must be between 0 and 100.");
        }

        exam.ExamApplicationStatus = dto.ExamApplicationStatus;
        exam.ExamApplicationDate = dto.ExamApplicationDate;
        exam.ExamApplicationDeadline = dto.ExamApplicationDeadline;
        exam.ExamDate = dto.ExamDate;
        exam.HallTicketStatus = dto.HallTicketStatus;
        exam.Status = dto.ExamStatus;
        exam.Score = dto.Score;
        exam.LastResultReminderDate = dto.Score.HasValue || !string.IsNullOrWhiteSpace(dto.PassStatus)
            ? null
            : (exam.ExamDate.HasValue ? exam.ExamDate.Value : null);

        if (!string.IsNullOrWhiteSpace(dto.PassStatus))
        {
            exam.PassStatus = dto.PassStatus;
        }
        else if (dto.Score.HasValue)
        {
            exam.PassStatus = dto.Score.Value >= 40 ? "Pass" : "Fail";
        }

        exam.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "ExamUpdated",
            $"Updated exam details for student '{exam.Registration?.Student?.RegisterNumber}' ({exam.Registration?.Student?.Name}) in course '{exam.Registration?.Course?.CourseCode}': AppStatus={exam.ExamApplicationStatus}, ExamStatus={exam.Status}, Score={exam.Score}, PassStatus={exam.PassStatus}",
            ipAddress,
            cancellationToken);

        return new AdminExamDto
        {
            ExamStatusId = exam.ExamStatusId,
            RegistrationId = exam.RegistrationId,
            StudentName = exam.Registration?.Student?.Name ?? string.Empty,
            RegisterNumber = exam.Registration?.Student?.RegisterNumber ?? string.Empty,
            CourseName = exam.Registration?.Course?.CourseName ?? string.Empty,
            ExamApplicationStatus = exam.ExamApplicationStatus ?? "NotStarted",
            ExamApplicationDate = exam.ExamApplicationDate,
            ExamApplicationDeadline = exam.ExamApplicationDeadline,
            ExamDate = exam.ExamDate,
            LastResultReminderDate = exam.LastResultReminderDate,
            HallTicketStatus = exam.HallTicketStatus,
            ExamStatus = exam.Status ?? "NotStarted",
            Score = exam.Score,
            PassStatus = exam.PassStatus
        };
    }
}
