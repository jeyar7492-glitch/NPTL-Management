using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using ExamStatusEntity = NPTELManagement.Core.Entities.ExamStatus;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class StudentCourseService : IStudentCourseService
{
    private readonly ApplicationDbContext _context;

    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue) return null;
        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    public StudentCourseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDashboardSummaryDto> GetDashboardSummaryAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var registrations = await _context.NptelRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .ToListAsync(cancellationToken);

        var summary = new StudentDashboardSummaryDto
        {
            RegisteredCourses = registrations.Count(r => r.Status == RegistrationStatus.Registered),
            InProgressCourses = registrations.Count(r => r.Status == RegistrationStatus.InProgress),
            CompletedCourses = registrations.Count(r => r.Status == RegistrationStatus.Completed),
            ExamPending = registrations.Count(r => r.ExamStatus != null && r.ExamStatus.Status != "Completed"),
            ExamCompleted = registrations.Count(r => r.ExamStatus != null && r.ExamStatus.Status == "Completed"),
            CertificatesPending = registrations.Count(r => r.Certificate != null && 
                (r.Certificate.VerifiedStatus == CertificateStatus.Pending || 
                 r.Certificate.VerifiedStatus == CertificateStatus.Submitted || 
                 r.Certificate.VerifiedStatus == CertificateStatus.UnderVerification)),
            CertificatesVerified = registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Verified),
            CertificatesReceived = registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Received)
        };

        return summary;
    }

    public async Task<List<StudentCourseDto>> GetStudentCoursesAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.NptelRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.EnrollmentDate)
            .Select(r => new StudentCourseDto
            {
                RegistrationId = r.RegistrationId,
                CourseId = r.CourseId,
                CourseCode = r.Course != null ? r.Course.CourseCode : string.Empty,
                CourseName = r.Course != null ? r.Course.CourseName : string.Empty,
                DurationWeeks = r.Course != null ? r.Course.DurationWeeks : 0,
                CourseStartDate = r.Course != null ? r.Course.CourseStartDate : null,
                CourseEndDate = r.Course != null ? r.Course.CourseEndDate : null,
                CourseCycle = r.Course != null ? r.Course.CourseCycle : null,
                ExamStartDate = r.Course != null ? r.Course.ExamStartDate : null,
                ExamEndDate = r.Course != null ? r.Course.ExamEndDate : null,
                CanEditDetails = r.Course != null && r.Course.CreatedByStudentId == studentId,
                EnrollmentDate = r.EnrollmentDate,
                RegistrationStatus = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AvailableCourseDto>> GetAvailableCoursesAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var registeredCourseIds = _context.NptelRegistrations
            .Where(r => r.StudentId == studentId)
            .Select(r => r.CourseId);

        return await _context.Courses
            .AsNoTracking()
            .Where(c => (c.CreatedByStudentId == null || c.CreatedByStudentId == studentId) &&
                        c.Status == "Active" &&
                        !registeredCourseIds.Contains(c.CourseId))
            .OrderBy(c => c.CourseName)
            .Select(c => new AvailableCourseDto
            {
                CourseId = c.CourseId,
                CourseCode = c.CourseCode,
                CourseName = c.CourseName,
                DurationWeeks = c.DurationWeeks,
                CourseStartDate = c.CourseStartDate,
                CourseEndDate = c.CourseEndDate,
                CourseCycle = c.CourseCycle,
                ExamStartDate = c.ExamStartDate,
                ExamEndDate = c.ExamEndDate
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentCourseDetailsDto> AddAndRegisterCourseAsync(
        Guid studentId,
        Guid userId,
        AddStudentCourseDto dto,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CourseCode))
            throw new ArgumentException("Course code is required.");
        if (string.IsNullOrWhiteSpace(dto.CourseName))
            throw new ArgumentException("Course name is required.");
        if (dto.DurationWeeks <= 0 || dto.DurationWeeks > 52)
            throw new ArgumentException("Duration must be between 1 and 52 weeks.");
        if (dto.CourseStartDate.HasValue && dto.CourseEndDate.HasValue && dto.CourseEndDate < dto.CourseStartDate)
            throw new ArgumentException("Course end date cannot be earlier than the start date.");

        dto.CourseStartDate = ToUtc(dto.CourseStartDate);
        dto.CourseEndDate = ToUtc(dto.CourseEndDate);
        dto.ExamStartDate = ToUtc(dto.ExamStartDate);
        dto.ExamEndDate = ToUtc(dto.ExamEndDate);

        var cleanCode = dto.CourseCode.Trim().ToUpperInvariant();
        var existing = await _context.Courses
            .FirstOrDefaultAsync(c => c.CreatedByStudentId == studentId &&
                                      c.CourseCode.ToUpper() == cleanCode,
                                  cancellationToken);

        if (existing != null)
        {
            var alreadyRegistered = await _context.NptelRegistrations
                .AnyAsync(r => r.StudentId == studentId && r.CourseId == existing.CourseId, cancellationToken);

            if (alreadyRegistered)
                throw new InvalidOperationException("You are already registered for this course.");

            return await RegisterForCourseAsync(studentId, existing.CourseId, userId, ipAddress, cancellationToken);
        }

        var course = new Course
        {
            CourseId = Guid.NewGuid(),
            CourseCode = cleanCode,
            CourseName = dto.CourseName.Trim(),
            DurationWeeks = dto.DurationWeeks,
            CourseStartDate = dto.CourseStartDate,
            CourseEndDate = dto.CourseEndDate,
            Status = "Active",
            CreatedByStudentId = studentId,
            CourseCycle = dto.CourseCycle?.Trim(),
            ExamStartDate = dto.ExamStartDate,
            ExamEndDate = dto.ExamEndDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        return await RegisterForCourseAsync(studentId, course.CourseId, userId, ipAddress, cancellationToken);
    }

    public async Task<StudentCourseDetailsDto> UpdateStudentCourseDetailsAsync(
        Guid studentId,
        Guid registrationId,
        UpdateStudentCourseDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CourseCode))
            throw new ArgumentException("Course code is required.");
        if (string.IsNullOrWhiteSpace(dto.CourseName))
            throw new ArgumentException("Course name is required.");
        if (dto.DurationWeeks <= 0 || dto.DurationWeeks > 52)
            throw new ArgumentException("Duration must be between 1 and 52 weeks.");
        if (dto.CourseEndDate.HasValue && dto.CourseStartDate.HasValue && dto.CourseEndDate < dto.CourseStartDate)
            throw new ArgumentException("Course end date cannot be earlier than the course start date.");
        dto.CourseStartDate = ToUtc(dto.CourseStartDate);
        dto.CourseEndDate = ToUtc(dto.CourseEndDate);
        dto.ExamStartDate = ToUtc(dto.ExamStartDate);
        dto.ExamEndDate = ToUtc(dto.ExamEndDate);

        if (dto.ExamEndDate.HasValue && dto.ExamStartDate.HasValue && dto.ExamEndDate < dto.ExamStartDate)
            throw new ArgumentException("Exam end date cannot be earlier than exam start date.");

        var registration = await _context.NptelRegistrations
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId && r.StudentId == studentId, cancellationToken);

        if (registration?.Course == null)
            throw new KeyNotFoundException("Course registration not found.");

        if (registration.Course.CreatedByStudentId != studentId)
            throw new UnauthorizedAccessException("Only the student who added this course can update its details.");

        var cleanCode = dto.CourseCode.Trim().ToUpperInvariant();
        var duplicate = await _context.Courses.AnyAsync(
            c => c.CourseId != registration.CourseId &&
                 c.CourseCode.ToUpper() == cleanCode,
            cancellationToken);

        if (duplicate)
            throw new InvalidOperationException($"Course code '{cleanCode}' is already in use.");

        registration.Course.CourseCode = cleanCode;
        registration.Course.CourseName = dto.CourseName.Trim();
        registration.Course.DurationWeeks = dto.DurationWeeks;
        registration.Course.CourseCycle = dto.CourseCycle?.Trim();
        registration.Course.CourseStartDate = dto.CourseStartDate;
        registration.Course.CourseEndDate = dto.CourseEndDate;
        registration.Course.ExamStartDate = dto.ExamStartDate;
        registration.Course.ExamEndDate = dto.ExamEndDate;
        registration.Course.UpdatedAt = DateTime.UtcNow;

        if (registration.ExamStatus != null)
        {
            registration.ExamStatus.ExamDate = dto.ExamStartDate;
            registration.ExamStatus.Status = dto.ExamStartDate.HasValue ? "Scheduled" : "NotStarted";
            registration.ExamStatus.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCourseDetailsAsync(studentId, registrationId, cancellationToken)
            ?? throw new KeyNotFoundException("Updated course could not be loaded.");
    }

    public async Task<StudentCourseDetailsDto> RegisterForCourseAsync(
        Guid studentId,
        Guid courseId,
        Guid userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken);
        if (course == null)
            throw new KeyNotFoundException("Selected course was not found.");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);
        if (student == null)
            throw new KeyNotFoundException("Student profile not found.");

        var exists = await _context.NptelRegistrations
            .AnyAsync(r => r.StudentId == studentId && r.CourseId == courseId, cancellationToken);

        if (exists)
            throw new InvalidOperationException("You are already registered for this course.");

        var registrationId = Guid.NewGuid();
        var enrollmentDate = DateTime.UtcNow;

        _context.NptelRegistrations.Add(new NptelRegistration
        {
            RegistrationId = registrationId,
            StudentId = studentId,
            CourseId = courseId,
            EnrollmentDate = enrollmentDate,
            Status = RegistrationStatus.Registered,
            CreatedAt = enrollmentDate,
            UpdatedAt = enrollmentDate
        });

        _context.ExamStatuses.Add(new ExamStatusEntity
        {
            ExamStatusId = Guid.NewGuid(),
            RegistrationId = registrationId,
            ExamApplicationStatus = "NotStarted",
            Status = "NotStarted",
            UpdatedAt = enrollmentDate
        });

        _context.Certificates.Add(new Certificate
        {
            CertificateId = Guid.NewGuid(),
            RegistrationId = registrationId,
            VerifiedStatus = CertificateStatus.Pending,
            CreatedAt = enrollmentDate,
            UpdatedAt = enrollmentDate
        });

        var timeline = new[]
        {
            new CourseTimeline
            {
                RegistrationId = registrationId,
                Title = "Course Registration Confirmed",
                Description = "Student registration completed and course access is active.",
                Status = "Completed",
                EventDate = enrollmentDate,
                DisplayOrder = 1,
                WeekNumber = 1
            },
            new CourseTimeline
            {
                RegistrationId = registrationId,
                Title = "Weekly Lectures & Assignments",
                Description = "Complete weekly lectures, quizzes and assignments.",
                Status = "Current",
                EventDate = course.CourseStartDate ?? enrollmentDate,
                DisplayOrder = 2,
                WeekNumber = 1
            },
            new CourseTimeline
            {
                RegistrationId = registrationId,
                Title = "Exam Registration",
                Description = "Apply for the NPTEL proctored examination when the registration window opens.",
                Status = "Pending",
                EventDate = course.CourseEndDate?.AddDays(7) ?? enrollmentDate.AddDays(course.DurationWeeks * 7 + 7),
                DisplayOrder = 3,
                WeekNumber = course.DurationWeeks + 1
            },
            new CourseTimeline
            {
                RegistrationId = registrationId,
                Title = "Proctored Examination",
                Description = "Attend the scheduled NPTEL examination.",
                Status = "Pending",
                EventDate = course.CourseEndDate?.AddDays(21) ?? enrollmentDate.AddDays(course.DurationWeeks * 7 + 21),
                DisplayOrder = 4,
                WeekNumber = course.DurationWeeks + 3
            },
            new CourseTimeline
            {
                RegistrationId = registrationId,
                Title = "Result & Certificate",
                Description = "Result and certificate will appear here after NPTEL publishes them.",
                Status = "Pending",
                EventDate = course.CourseEndDate?.AddDays(45) ?? enrollmentDate.AddDays(course.DurationWeeks * 7 + 45),
                DisplayOrder = 5,
                WeekNumber = course.DurationWeeks + 6
            }
        };

        _context.CourseTimelines.AddRange(timeline);
        await _context.SaveChangesAsync(cancellationToken);

        var details = await GetCourseDetailsAsync(studentId, registrationId, cancellationToken);
        if (details == null)
            throw new InvalidOperationException("Course registration was created but details could not be loaded.");

        return details;
    }

    public async Task<StudentCourseDetailsDto?> GetCourseDetailsAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default)
    {
        // Enforce student ownership: registration must belong to studentId
        var reg = await _context.NptelRegistrations
            .AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .Include(r => r.Timeline)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId && r.StudentId == studentId, cancellationToken);

        if (reg == null)
        {
            return null;
        }

        var details = new StudentCourseDetailsDto
        {
            RegistrationId = reg.RegistrationId,
            CourseId = reg.CourseId,
            CourseCode = reg.Course?.CourseCode ?? string.Empty,
            CourseName = reg.Course?.CourseName ?? string.Empty,
            DurationWeeks = reg.Course?.DurationWeeks ?? 0,
            CourseStartDate = reg.Course?.CourseStartDate,
            CourseEndDate = reg.Course?.CourseEndDate,
            CourseCycle = reg.Course?.CourseCycle,
            ExamStartDate = reg.Course?.ExamStartDate,
            ExamEndDate = reg.Course?.ExamEndDate,
            CanEditDetails = reg.Course?.CreatedByStudentId == studentId,
            EnrollmentDate = reg.EnrollmentDate,
            RegistrationStatus = reg.Status.ToString(),
            Timeline = reg.Timeline
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.CreatedAt)
                .Select(t => new StudentTimelineItemDto
                {
                    TimelineId = t.TimelineId,
                    Title = t.Title,
                    Status = t.Status,
                    Description = t.Description,
                    EventDate = t.EventDate,
                    DisplayOrder = t.DisplayOrder
                })
                .ToList()
        };

        if (reg.ExamStatus != null)
        {
            details.Exam = new StudentExamDto
            {
                ExamApplicationStatus = reg.ExamStatus.ExamApplicationStatus,
                ExamApplicationDate = reg.ExamStatus.ExamApplicationDate,
                ExamApplicationDeadline = reg.ExamStatus.ExamApplicationDeadline,
                ExamDate = reg.ExamStatus.ExamDate,
                HallTicketStatus = reg.ExamStatus.HallTicketStatus,
                ExamStatus = reg.ExamStatus.Status,
                Score = reg.ExamStatus.Score,
                PassStatus = reg.ExamStatus.PassStatus
            };
        }

        if (reg.Certificate != null)
        {
            details.Certificate = new StudentCertificateDto
            {
                CertificateId = reg.Certificate.CertificateId,
                VerifiedStatus = reg.Certificate.VerifiedStatus.ToString(),
                StoragePath = reg.Certificate.StoragePath,
                CertificateNumber = reg.Certificate.CertificateNumber,
                Score = reg.Certificate.Score,
                PassStatus = reg.Certificate.PassStatus,
                SubmittedDate = reg.Certificate.SubmittedDate,
                IssuedDate = reg.Certificate.IssuedDate,
                VerifiedDate = reg.Certificate.VerifiedDate,
                ReceivedDate = reg.Certificate.ReceivedDate,
                ReminderEnabled = reg.Certificate.ReminderEnabled,
                ReminderDate = reg.Certificate.ReminderDate
            };
        }

        return details;
    }

    public async Task<List<StudentTimelineItemDto>?> GetCourseTimelineAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default)
    {
        // Enforce ownership
        var isOwned = await _context.NptelRegistrations
            .AsNoTracking()
            .AnyAsync(r => r.RegistrationId == registrationId && r.StudentId == studentId, cancellationToken);

        if (!isOwned)
        {
            return null;
        }

        return await _context.CourseTimelines
            .AsNoTracking()
            .Where(t => t.RegistrationId == registrationId)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new StudentTimelineItemDto
            {
                TimelineId = t.TimelineId,
                Title = t.Title,
                Status = t.Status,
                Description = t.Description,
                EventDate = t.EventDate,
                DisplayOrder = t.DisplayOrder
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentExamDto?> GetCourseExamStatusAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default)
    {
        // Enforce ownership
        var reg = await _context.NptelRegistrations
            .AsNoTracking()
            .Include(r => r.ExamStatus)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId && r.StudentId == studentId, cancellationToken);

        if (reg == null)
        {
            return null;
        }

        if (reg.ExamStatus == null)
        {
            return new StudentExamDto();
        }

        return new StudentExamDto
        {
            ExamApplicationStatus = reg.ExamStatus.ExamApplicationStatus,
            ExamApplicationDate = reg.ExamStatus.ExamApplicationDate,
            ExamApplicationDeadline = reg.ExamStatus.ExamApplicationDeadline,
            ExamDate = reg.ExamStatus.ExamDate,
            HallTicketStatus = reg.ExamStatus.HallTicketStatus,
            ExamStatus = reg.ExamStatus.Status,
            Score = reg.ExamStatus.Score,
            PassStatus = reg.ExamStatus.PassStatus
        };
    }

    public async Task<StudentCertificateDto?> GetCourseCertificateStatusAsync(Guid studentId, Guid registrationId, CancellationToken cancellationToken = default)
    {
        // Enforce ownership
        var reg = await _context.NptelRegistrations
            .AsNoTracking()
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId && r.StudentId == studentId, cancellationToken);

        if (reg == null)
        {
            return null;
        }

        if (reg.Certificate == null)
        {
            return null;
        }

        return new StudentCertificateDto
        {
            CertificateId = reg.Certificate.CertificateId,
            VerifiedStatus = reg.Certificate.VerifiedStatus.ToString(),
            StoragePath = reg.Certificate.StoragePath,
            CertificateNumber = reg.Certificate.CertificateNumber,
            Score = reg.Certificate.Score,
            PassStatus = reg.Certificate.PassStatus,
            SubmittedDate = reg.Certificate.SubmittedDate,
            IssuedDate = reg.Certificate.IssuedDate,
            VerifiedDate = reg.Certificate.VerifiedDate,
            ReceivedDate = reg.Certificate.ReceivedDate,
            ReminderEnabled = reg.Certificate.ReminderEnabled,
            ReminderDate = reg.Certificate.ReminderDate
        };
    }
}
