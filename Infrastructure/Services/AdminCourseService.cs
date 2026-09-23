using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AdminCourseService : IAdminCourseService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminCourseService(ApplicationDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminCourseDto>> GetCoursesAsync(
        string? search, 
        string? status, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Courses
            .Include(c => c.Registrations)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.CourseCode.ToLower().Contains(s) || c.CourseName.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status.ToLower() == status.Trim().ToLower());
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCourseDto
            {
                CourseId = c.CourseId,
                CourseCode = c.CourseCode,
                CourseName = c.CourseName,
                DurationWeeks = c.DurationWeeks,
                CourseStartDate = c.CourseStartDate,
                CourseEndDate = c.CourseEndDate,
                Status = c.Status,
                TotalRegistrations = c.Registrations.Count,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCourseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminCourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var c = await _context.Courses
            .Include(c => c.Registrations)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken);

        if (c == null) return null;

        return new AdminCourseDto
        {
            CourseId = c.CourseId,
            CourseCode = c.CourseCode,
            CourseName = c.CourseName,
            DurationWeeks = c.DurationWeeks,
            CourseStartDate = c.CourseStartDate,
            CourseEndDate = c.CourseEndDate,
            Status = c.Status,
            TotalRegistrations = c.Registrations.Count,
            CreatedAt = c.CreatedAt
        };
    }

    public async Task<AdminCourseDto> CreateCourseAsync(
        CreateCourseDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CourseCode))
            throw new ArgumentException("Course code is required.");
        if (string.IsNullOrWhiteSpace(dto.CourseName))
            throw new ArgumentException("Course name is required.");
        if (dto.DurationWeeks <= 0)
            throw new ArgumentException("Duration weeks must be greater than zero.");
        if (dto.CourseStartDate.HasValue && dto.CourseEndDate.HasValue && dto.CourseEndDate < dto.CourseStartDate)
            throw new ArgumentException("Course end date cannot be earlier than course start date.");

        var cleanCode = dto.CourseCode.Trim().ToUpperInvariant();
        var exists = await _context.Courses.AnyAsync(c => c.CourseCode.ToUpper() == cleanCode, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"Course code '{cleanCode}' already exists.");

        var course = new Course
        {
            CourseId = Guid.NewGuid(),
            CourseCode = cleanCode,
            CourseName = dto.CourseName.Trim(),
            DurationWeeks = dto.DurationWeeks,
            CourseStartDate = dto.CourseStartDate,
            CourseEndDate = dto.CourseEndDate,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "CourseCreated",
            $"Created course '{course.CourseCode}' - '{course.CourseName}', Duration: {course.DurationWeeks}w, Status: {course.Status}",
            ipAddress,
            cancellationToken);

        return new AdminCourseDto
        {
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            DurationWeeks = course.DurationWeeks,
            CourseStartDate = course.CourseStartDate,
            CourseEndDate = course.CourseEndDate,
            Status = course.Status,
            TotalRegistrations = 0,
            CreatedAt = course.CreatedAt
        };
    }

    public async Task<AdminCourseDto> UpdateCourseAsync(
        Guid courseId, 
        UpdateCourseDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .Include(c => c.Registrations)
            .FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken);

        if (course == null)
            throw new KeyNotFoundException($"Course with ID {courseId} not found.");

        if (string.IsNullOrWhiteSpace(dto.CourseName))
            throw new ArgumentException("Course name is required.");
        if (dto.DurationWeeks <= 0)
            throw new ArgumentException("Duration weeks must be greater than zero.");
        if (dto.CourseStartDate.HasValue && dto.CourseEndDate.HasValue && dto.CourseEndDate < dto.CourseStartDate)
            throw new ArgumentException("Course end date cannot be earlier than course start date.");

        var oldName = course.CourseName;
        var oldDuration = course.DurationWeeks;
        var oldStatus = course.Status;

        course.CourseName = dto.CourseName.Trim();
        course.DurationWeeks = dto.DurationWeeks;
        course.CourseStartDate = dto.CourseStartDate;
        course.CourseEndDate = dto.CourseEndDate;
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            course.Status = dto.Status.Trim();
        }
        course.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "CourseUpdated",
            $"Updated course {course.CourseCode}: Name '{oldName}'->'{course.CourseName}', Duration {oldDuration}->{course.DurationWeeks}w, Status {oldStatus}->{course.Status}",
            ipAddress,
            cancellationToken);

        return new AdminCourseDto
        {
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            DurationWeeks = course.DurationWeeks,
            CourseStartDate = course.CourseStartDate,
            CourseEndDate = course.CourseEndDate,
            Status = course.Status,
            TotalRegistrations = course.Registrations.Count,
            CreatedAt = course.CreatedAt
        };
    }

    public async Task<bool> UpdateCourseStatusAsync(
        Guid courseId, 
        string status, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken);
        if (course == null) return false;

        var oldStatus = course.Status;
        course.Status = status.Trim();
        course.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "CourseStatusChanged",
            $"Changed course {course.CourseCode} status from '{oldStatus}' to '{course.Status}'",
            ipAddress,
            cancellationToken);

        return true;
    }
}
