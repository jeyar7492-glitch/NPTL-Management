using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class StudentCourseService : IStudentCourseService
{
    private readonly ApplicationDbContext _context;

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
                EnrollmentDate = r.EnrollmentDate,
                RegistrationStatus = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);
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
                SubmittedDate = reg.Certificate.SubmittedDate,
                IssuedDate = reg.Certificate.IssuedDate,
                VerifiedDate = reg.Certificate.VerifiedDate,
                ReceivedDate = reg.Certificate.ReceivedDate
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
            SubmittedDate = reg.Certificate.SubmittedDate,
            IssuedDate = reg.Certificate.IssuedDate,
            VerifiedDate = reg.Certificate.VerifiedDate,
            ReceivedDate = reg.Certificate.ReceivedDate
        };
    }
}
