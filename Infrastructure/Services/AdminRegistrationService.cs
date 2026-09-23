using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;
using ExamStatusEntity = NPTELManagement.Core.Entities.ExamStatus;

namespace NPTELManagement.Infrastructure.Services;

public class AdminRegistrationService : IAdminRegistrationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminRegistrationService(ApplicationDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminRegistrationDto>> GetRegistrationsAsync(
        string? search, 
        string? status, 
        Guid? studentId, 
        Guid? courseId, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .AsNoTracking()
            .AsQueryable();

        if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            query = query.Where(r => r.StudentId == studentId.Value);
        }

        if (courseId.HasValue && courseId.Value != Guid.Empty)
        {
            query = query.Where(r => r.CourseId == courseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<RegistrationStatus>(status.Trim(), true, out var parsedStatus))
            {
                query = query.Where(r => r.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r => 
                (r.Student != null && (r.Student.Name.ToLower().Contains(s) || r.Student.RegisterNumber.ToLower().Contains(s))) ||
                (r.Course != null && (r.Course.CourseName.ToLower().Contains(s) || r.Course.CourseCode.ToLower().Contains(s))));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.EnrollmentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminRegistrationDto
            {
                RegistrationId = r.RegistrationId,
                StudentId = r.StudentId,
                StudentName = r.Student != null ? r.Student.Name : string.Empty,
                RegisterNumber = r.Student != null ? r.Student.RegisterNumber : string.Empty,
                Department = r.Student != null ? r.Student.Department : string.Empty,
                Year = r.Student != null ? r.Student.Year : 0,
                ClassSection = r.Student != null ? r.Student.ClassSection : null,
                CourseId = r.CourseId,
                CourseCode = r.Course != null ? r.Course.CourseCode : string.Empty,
                CourseName = r.Course != null ? r.Course.CourseName : string.Empty,
                DurationWeeks = r.Course != null ? r.Course.DurationWeeks : 0,
                EnrollmentDate = r.EnrollmentDate,
                Status = r.Status.ToString(),
                ExamApplicationStatus = r.ExamStatus != null ? (r.ExamStatus.ExamApplicationStatus ?? "NotStarted") : "NotStarted",
                CertificateStatus = r.Certificate != null ? r.Certificate.VerifiedStatus.ToString() : "Pending"
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminRegistrationDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminRegistrationDto?> GetRegistrationByIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var r = await _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);

        if (r == null) return null;

        return new AdminRegistrationDto
        {
            RegistrationId = r.RegistrationId,
            StudentId = r.StudentId,
            StudentName = r.Student != null ? r.Student.Name : string.Empty,
            RegisterNumber = r.Student != null ? r.Student.RegisterNumber : string.Empty,
            Department = r.Student != null ? r.Student.Department : string.Empty,
            Year = r.Student != null ? r.Student.Year : 0,
            ClassSection = r.Student != null ? r.Student.ClassSection : null,
            CourseId = r.CourseId,
            CourseCode = r.Course != null ? r.Course.CourseCode : string.Empty,
            CourseName = r.Course != null ? r.Course.CourseName : string.Empty,
            DurationWeeks = r.Course != null ? r.Course.DurationWeeks : 0,
            EnrollmentDate = r.EnrollmentDate,
            Status = r.Status.ToString(),
            ExamApplicationStatus = r.ExamStatus != null ? (r.ExamStatus.ExamApplicationStatus ?? "NotStarted") : "NotStarted",
            CertificateStatus = r.Certificate != null ? r.Certificate.VerifiedStatus.ToString() : "Pending"
        };
    }

    public async Task<AdminRegistrationDto> CreateRegistrationAsync(
        CreateRegistrationDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentId == dto.StudentId, cancellationToken);
        if (student == null)
            throw new KeyNotFoundException($"Student with ID {dto.StudentId} not found.");

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.CourseId == dto.CourseId, cancellationToken);
        if (course == null)
            throw new KeyNotFoundException($"Course with ID {dto.CourseId} not found.");

        var exists = await _context.NptelRegistrations
            .AnyAsync(r => r.StudentId == dto.StudentId && r.CourseId == dto.CourseId, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"Student '{student.RegisterNumber}' is already registered for course '{course.CourseCode}'.");

        var registrationStatus = RegistrationStatus.Registered;
        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<RegistrationStatus>(dto.Status.Trim(), true, out var parsed))
        {
            registrationStatus = parsed;
        }

        var enrollmentDate = dto.EnrollmentDate ?? DateTime.UtcNow;
        var regId = Guid.NewGuid();

        var registration = new NptelRegistration
        {
            RegistrationId = regId,
            StudentId = dto.StudentId,
            CourseId = dto.CourseId,
            EnrollmentDate = enrollmentDate,
            Status = registrationStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var examStatus = new ExamStatusEntity
        {
            ExamStatusId = Guid.NewGuid(),
            RegistrationId = regId,
            ExamApplicationStatus = "NotStarted",
            Status = "NotStarted",
            UpdatedAt = DateTime.UtcNow
        };

        var cert = new Certificate
        {
            CertificateId = Guid.NewGuid(),
            RegistrationId = regId,
            VerifiedStatus = CertificateStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 11 baseline milestones for course timeline
        var timelines = GenerateDefaultTimeline(regId, course.DurationWeeks, enrollmentDate);

        _context.NptelRegistrations.Add(registration);
        _context.ExamStatuses.Add(examStatus);
        _context.Certificates.Add(cert);
        _context.CourseTimelines.AddRange(timelines);

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "RegistrationCreated",
            $"Enrolled student '{student.RegisterNumber}' ({student.Name}) into course '{course.CourseCode}' ({course.CourseName})",
            ipAddress,
            cancellationToken);

        return new AdminRegistrationDto
        {
            RegistrationId = regId,
            StudentId = student.StudentId,
            StudentName = student.Name,
            RegisterNumber = student.RegisterNumber,
            Department = student.Department,
            Year = student.Year,
            ClassSection = student.ClassSection,
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            DurationWeeks = course.DurationWeeks,
            EnrollmentDate = enrollmentDate,
            Status = registrationStatus.ToString(),
            ExamApplicationStatus = examStatus.ExamApplicationStatus ?? "NotStarted",
            CertificateStatus = cert.VerifiedStatus.ToString()
        };
    }

    public async Task<AdminRegistrationDto> UpdateRegistrationStatusAsync(
        Guid registrationId, 
        UpdateRegistrationDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var registration = await _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);

        if (registration == null)
            throw new KeyNotFoundException($"Registration with ID {registrationId} not found.");

        if (!Enum.TryParse<RegistrationStatus>(dto.Status.Trim(), true, out var newStatus))
            throw new ArgumentException($"Invalid registration status '{dto.Status}'. Allowed: Registered, Active, InProgress, Completed, Dropped.");

        var oldStatus = registration.Status;
        registration.Status = newStatus;
        registration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "RegistrationStatusUpdated",
            $"Updated registration {registrationId} status from '{oldStatus}' to '{newStatus}' for student '{registration.Student?.RegisterNumber}' in course '{registration.Course?.CourseCode}'",
            ipAddress,
            cancellationToken);

        return new AdminRegistrationDto
        {
            RegistrationId = registration.RegistrationId,
            StudentId = registration.StudentId,
            StudentName = registration.Student?.Name ?? string.Empty,
            RegisterNumber = registration.Student?.RegisterNumber ?? string.Empty,
            Department = registration.Student?.Department ?? string.Empty,
            Year = registration.Student?.Year ?? 0,
            ClassSection = registration.Student?.ClassSection,
            CourseId = registration.CourseId,
            CourseCode = registration.Course?.CourseCode ?? string.Empty,
            CourseName = registration.Course?.CourseName ?? string.Empty,
            DurationWeeks = registration.Course?.DurationWeeks ?? 0,
            EnrollmentDate = registration.EnrollmentDate,
            Status = registration.Status.ToString(),
            ExamApplicationStatus = registration.ExamStatus?.ExamApplicationStatus ?? "NotStarted",
            CertificateStatus = registration.Certificate?.VerifiedStatus.ToString() ?? "Pending"
        };
    }

    private static List<CourseTimeline> GenerateDefaultTimeline(Guid registrationId, int durationWeeks, DateTime startDate)
    {
        var list = new List<CourseTimeline>
        {
            new()
            {
                RegistrationId = registrationId,
                Title = "Course Orientation & Enrollment",
                Description = "Enrollment confirmed and course content accessible.",
                Status = "Completed",
                EventDate = startDate,
                DisplayOrder = 1,
                WeekNumber = 1
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Week 1 - 4 Lecture & Assignment Period",
                Description = "Initial modules, weekly quizzes, and assignments.",
                Status = "InProgress",
                EventDate = startDate.AddDays(28),
                DisplayOrder = 2,
                WeekNumber = 4
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Exam Registration Window Opens",
                Description = "Portal open for proctored examination registration and center choice.",
                Status = "Pending",
                EventDate = startDate.AddDays(35),
                DisplayOrder = 3,
                WeekNumber = 5
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Week 5 - 8 Intermediate Progress",
                Description = "Mid-term course topics and programming/theoretical assignments.",
                Status = "Pending",
                EventDate = startDate.AddDays(56),
                DisplayOrder = 4,
                WeekNumber = 8
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Exam Registration Deadline",
                Description = "Last date to submit exam fees and lock examination slot.",
                Status = "Pending",
                EventDate = startDate.AddDays(63),
                DisplayOrder = 5,
                WeekNumber = 9
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Week 9 - 12 Advanced Topics & Final Prep",
                Description = "Final assignments, wrap-up lectures, and course review.",
                Status = "Pending",
                EventDate = startDate.AddDays(durationWeeks * 7),
                DisplayOrder = 6,
                WeekNumber = durationWeeks
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Hall Ticket Release",
                Description = "NPTEL releases admit cards for registered candidates.",
                Status = "Pending",
                EventDate = startDate.AddDays(durationWeeks * 7 + 10),
                DisplayOrder = 7,
                WeekNumber = durationWeeks + 1
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Proctored Examination",
                Description = "Final computer-based / pen-and-paper examination at designated test center.",
                Status = "Pending",
                EventDate = startDate.AddDays(durationWeeks * 7 + 17),
                DisplayOrder = 8,
                WeekNumber = durationWeeks + 2
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "Exam Results Announcement",
                Description = "NPTEL publishes final exam scores and assignment consolidated marks.",
                Status = "Pending",
                EventDate = startDate.AddDays(durationWeeks * 7 + 35),
                DisplayOrder = 9,
                WeekNumber = durationWeeks + 5
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "E-Certificate Issuance",
                Description = "Digital verifiable certificate published on NPTEL portal.",
                Status = "Pending",
                EventDate = startDate.AddDays(durationWeeks * 7 + 45),
                DisplayOrder = 10,
                WeekNumber = durationWeeks + 6
            },
            new()
            {
                RegistrationId = registrationId,
                Title = "College Verification & Credit Transfer",
                Description = "Staff and admin verify certificate and award department credit.",
                Status = "Pending",
                EventDate = startDate.AddDays(durationWeeks * 7 + 55),
                DisplayOrder = 11,
                WeekNumber = durationWeeks + 7
            }
        };

        return list;
    }
}
