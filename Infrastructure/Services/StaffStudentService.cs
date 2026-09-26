using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class StaffStudentService : IStaffStudentService
{
    private readonly ApplicationDbContext _context;
    private readonly IStaffAuthorizationService _staffAuthService;

    public StaffStudentService(ApplicationDbContext context, IStaffAuthorizationService staffAuthService)
    {
        _context = context;
        _staffAuthService = staffAuthService;
    }

    private async Task<Staff?> GetStaffContextAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        return await _context.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == staffUserId || s.StaffId == staffUserId, cancellationToken);
    }

    public async Task<StaffProfileResponse?> GetStaffProfileAsync(Guid staffUserId, CancellationToken cancellationToken = default)
    {
        var staff = await GetStaffContextAsync(staffUserId, cancellationToken);
        if (staff == null)
            return null;

        var studentCount = await GetScopedStudentQuery(staff).CountAsync(cancellationToken);

        return new StaffProfileResponse
        {
            StaffId = staff.StaffId,
            StaffName = staff.StaffName,
            StaffIdentifier = staff.StaffIdentifier,
            Department = staff.Department,
            AssignedYear = staff.AssignedYear,
            AssignedClass = staff.AssignedClass,
            AssignedStudentCount = studentCount
        };
    }

    public async Task<StaffDashboardSummaryDto> GetDashboardSummaryAsync(Guid staffUserId, CancellationToken cancellationToken = default)
    {
        var staff = await GetStaffContextAsync(staffUserId, cancellationToken);
        if (staff == null)
            return new StaffDashboardSummaryDto();

        var studentIds = await GetScopedStudentQuery(staff)
            .Select(s => s.StudentId)
            .ToListAsync(cancellationToken);

        var totalStudents = studentIds.Count;
        if (totalStudents == 0)
        {
            return new StaffDashboardSummaryDto();
        }

        var registrations = await _context.NptelRegistrations
            .AsNoTracking()
            .Where(r => studentIds.Contains(r.StudentId))
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .ToListAsync(cancellationToken);

        return new StaffDashboardSummaryDto
        {
            TotalStudents = totalStudents,
            RegisteredStudents = registrations.Select(r => r.StudentId).Distinct().Count(),
            InProgressCourses = registrations.Count(r => r.Status == RegistrationStatus.InProgress),
            CompletedCourses = registrations.Count(r => r.Status == RegistrationStatus.Completed),
            ExamPending = registrations.Count(r => r.ExamStatus != null && r.ExamStatus.Status != "Completed"),
            ExamApplied = registrations.Count(r => r.ExamStatus != null && r.ExamStatus.ExamApplicationStatus == "Applied"),
            ExamCompleted = registrations.Count(r => r.ExamStatus != null && r.ExamStatus.Status == "Completed"),
            CertificatePending = registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Pending),
            CertificateSubmitted = registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Submitted),
            CertificateVerified = registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Verified),
            CertificateReceived = registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Received)
        };
    }

    public async Task<PagedResult<StaffStudentListDto>> GetAssignedStudentsAsync(
        Guid staffUserId, 
        StaffStudentFilterDto filter, 
        CancellationToken cancellationToken = default)
    {
        var staff = await GetStaffContextAsync(staffUserId, cancellationToken);
        if (staff == null)
            return new PagedResult<StaffStudentListDto>();

        // 1. Strict Server-Side Scope check on Year
        if (filter.Year.HasValue && filter.Year.Value != staff.AssignedYear)
        {
            // Client requested a year outside staff assignment -> cannot expand scope -> empty result
            return new PagedResult<StaffStudentListDto>
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = 0,
                Items = new List<StaffStudentListDto>()
            };
        }

        // 2. Strict Server-Side Scope check on Class Section
        if (!string.IsNullOrWhiteSpace(filter.ClassSection) && 
            !string.IsNullOrWhiteSpace(staff.AssignedClass) &&
            !string.Equals(staff.AssignedClass, filter.ClassSection, StringComparison.OrdinalIgnoreCase))
        {
            // Client requested class outside staff assignment -> empty result
            return new PagedResult<StaffStudentListDto>
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = 0,
                Items = new List<StaffStudentListDto>()
            };
        }

        var query = GetScopedStudentQuery(staff);

        // Filter by class section if staff is whole-year or same class
        if (!string.IsNullOrWhiteSpace(filter.ClassSection))
        {
            var cTerm = filter.ClassSection.Trim().ToUpper();
            query = query.Where(s => s.ClassSection != null && s.ClassSection.ToUpper() == cTerm);
        }

        // Search text
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var sTerm = filter.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(sTerm) ||
                s.RegisterNumber.ToLower().Contains(sTerm) ||
                (s.ClassSection != null && s.ClassSection.ToLower().Contains(sTerm)) ||
                (s.Batch != null && s.Batch.ToLower().Contains(sTerm))
            );
        }

        // Course filter
        if (!string.IsNullOrWhiteSpace(filter.Course))
        {
            var crsTerm = filter.Course.Trim().ToLower();
            query = query.Where(s => s.Registrations.Any(r => 
                r.Course != null && (r.Course.CourseName.ToLower().Contains(crsTerm) || r.Course.CourseCode.ToLower().Contains(crsTerm))
            ));
        }

        // Registration status filter
        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus) && filter.RegistrationStatus != "All")
        {
            if (Enum.TryParse<RegistrationStatus>(filter.RegistrationStatus, true, out var regStatus))
            {
                query = query.Where(s => s.Registrations.Any(r => r.Status == regStatus));
            }
        }

        // Exam status filter
        if (!string.IsNullOrWhiteSpace(filter.ExamStatus) && filter.ExamStatus != "All")
        {
            var eStatus = filter.ExamStatus.Trim().ToLower();
            query = query.Where(s => s.Registrations.Any(r => 
                r.ExamStatus != null && (
                    (r.ExamStatus.Status != null && r.ExamStatus.Status.ToLower() == eStatus) ||
                    (r.ExamStatus.ExamApplicationStatus != null && r.ExamStatus.ExamApplicationStatus.ToLower() == eStatus)
                )
            ));
        }

        // Certificate status filter
        if (!string.IsNullOrWhiteSpace(filter.CertificateStatus) && filter.CertificateStatus != "All")
        {
            if (Enum.TryParse<CertificateStatus>(filter.CertificateStatus, true, out var certStatus))
            {
                query = query.Where(s => s.Registrations.Any(r => r.Certificate != null && r.Certificate.VerifiedStatus == certStatus));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Clamped pagination
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var students = await query
            .OrderBy(s => s.RegisterNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.ExamStatus)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Certificate)
            .ToListAsync(cancellationToken);

        var items = students.Select(s =>
        {
            var regCount = s.Registrations.Count;
            var inProgCount = s.Registrations.Count(r => r.Status == RegistrationStatus.InProgress);
            var examPendingCount = s.Registrations.Count(r => r.ExamStatus != null && r.ExamStatus.Status != "Completed");
            var certPendingCount = s.Registrations.Count(r => r.Certificate != null && r.Certificate.VerifiedStatus == CertificateStatus.Pending);

            var firstReg = s.Registrations.FirstOrDefault();
            var examSummary = firstReg?.ExamStatus?.ExamApplicationStatus 
                ?? firstReg?.ExamStatus?.Status 
                ?? "NotStarted";

            var certSummary = firstReg?.Certificate?.VerifiedStatus.ToString() ?? "Pending";

            return new StaffStudentListDto
            {
                StudentId = s.StudentId,
                Name = s.Name,
                RegisterNumber = s.RegisterNumber,
                Department = s.Department,
                ClassSection = s.ClassSection,
                Year = s.Year,
                Batch = s.Batch,
                Email = s.Email,
                Phone = s.Phone,
                RegisteredCourseCount = regCount,
                InProgressCount = inProgCount,
                ExamPending = examPendingCount,
                CertificatePending = certPendingCount,
                ExamStatusSummary = examSummary,
                CertificateStatusSummary = certSummary
            };
        }).ToList();

        return new PagedResult<StaffStudentListDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<StaffStudentDetailsDto?> GetStudentDetailsAsync(Guid staffUserId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var isAuthorized = await _staffAuthService.CanStaffAccessStudentAsync(staffUserId, studentId, cancellationToken);
        if (!isAuthorized)
            return null;

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Course)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.ExamStatus)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Certificate)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Timeline)
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        if (student == null)
            return null;

        var details = new StaffStudentDetailsDto
        {
            StudentId = student.StudentId,
            Name = student.Name,
            RegisterNumber = student.RegisterNumber,
            Department = student.Department,
            ClassSection = student.ClassSection,
            Year = student.Year,
            Batch = student.Batch,
            Email = student.Email,
            Phone = student.Phone,
            Courses = student.Registrations.Select(r =>
            {
                var currentTimeline = r.Timeline
                    .OrderBy(t => t.DisplayOrder)
                    .FirstOrDefault(t => t.Status == "Current")?.Title;

                return new StaffStudentCourseDto
                {
                    RegistrationId = r.RegistrationId,
                    CourseId = r.CourseId,
                    CourseName = r.Course?.CourseName ?? string.Empty,
                    CourseCode = r.Course?.CourseCode ?? string.Empty,
                    DurationWeeks = r.Course?.DurationWeeks ?? 0,
                    RegistrationDate = r.EnrollmentDate,
                    CourseStartDate = r.Course?.CourseStartDate,
                    CourseEndDate = r.Course?.CourseEndDate,
                    RegistrationStatus = r.Status.ToString(),
                    CurrentTimelineStatus = currentTimeline,
                    Exam = r.ExamStatus != null ? new StudentExamDto
                    {
                        ExamApplicationStatus = r.ExamStatus.ExamApplicationStatus,
                        ExamApplicationDate = r.ExamStatus.ExamApplicationDate,
                        ExamApplicationDeadline = r.ExamStatus.ExamApplicationDeadline,
                        ExamDate = r.ExamStatus.ExamDate,
                        HallTicketStatus = r.ExamStatus.HallTicketStatus,
                        ExamStatus = r.ExamStatus.Status,
                        Score = r.ExamStatus.Score,
                        PassStatus = r.ExamStatus.PassStatus
                    } : null,
                    Certificate = r.Certificate != null ? new StudentCertificateDto
                    {
                        CertificateId = r.Certificate.CertificateId,
                        VerifiedStatus = r.Certificate.VerifiedStatus.ToString(),
                        StoragePath = r.Certificate.StoragePath,
                        CertificateNumber = r.Certificate.CertificateNumber,
                        Score = r.Certificate.Score,
                        PassStatus = r.Certificate.PassStatus,
                        SubmittedDate = r.Certificate.SubmittedDate,
                        IssuedDate = r.Certificate.IssuedDate,
                        VerifiedDate = r.Certificate.VerifiedDate,
                        ReceivedDate = r.Certificate.ReceivedDate,
                        ReminderEnabled = r.Certificate.ReminderEnabled,
                        ReminderDate = r.Certificate.ReminderDate
                    } : null
                };
            }).ToList()
        };

        return details;
    }

    public async Task<List<StaffStudentCourseDto>?> GetStudentCoursesAsync(Guid staffUserId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var details = await GetStudentDetailsAsync(staffUserId, studentId, cancellationToken);
        return details?.Courses;
    }

    public async Task<List<StudentTimelineItemDto>?> GetStudentTimelineAsync(Guid staffUserId, Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default)
    {
        var isAuthorized = await _staffAuthService.CanStaffAccessStudentAsync(staffUserId, studentId, cancellationToken);
        if (!isAuthorized)
            return null;

        var regQuery = _context.NptelRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId);

        if (registrationId.HasValue)
        {
            regQuery = regQuery.Where(r => r.RegistrationId == registrationId.Value);
        }

        var reg = await regQuery
            .Include(r => r.Timeline)
            .FirstOrDefaultAsync(cancellationToken);

        if (reg == null)
            return new List<StudentTimelineItemDto>();

        return reg.Timeline
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
            .ToList();
    }

    public async Task<StudentExamDto?> GetStudentExamStatusAsync(Guid staffUserId, Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default)
    {
        var isAuthorized = await _staffAuthService.CanStaffAccessStudentAsync(staffUserId, studentId, cancellationToken);
        if (!isAuthorized)
            return null;

        var regQuery = _context.NptelRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId);

        if (registrationId.HasValue)
        {
            regQuery = regQuery.Where(r => r.RegistrationId == registrationId.Value);
        }

        var reg = await regQuery
            .Include(r => r.ExamStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (reg?.ExamStatus == null)
            return new StudentExamDto();

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

    public async Task<StudentCertificateDto?> GetStudentCertificateStatusAsync(Guid staffUserId, Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default)
    {
        var isAuthorized = await _staffAuthService.CanStaffAccessStudentAsync(staffUserId, studentId, cancellationToken);
        if (!isAuthorized)
            return null;

        var regQuery = _context.NptelRegistrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId);

        if (registrationId.HasValue)
        {
            regQuery = regQuery.Where(r => r.RegistrationId == registrationId.Value);
        }

        var reg = await regQuery
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(cancellationToken);

        if (reg?.Certificate == null)
            return new StudentCertificateDto();

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

    private IQueryable<Student> GetScopedStudentQuery(Staff staff)
    {
        var query = _context.Students
            .AsNoTracking()
            .Where(s => s.Department.ToUpper() == staff.Department.ToUpper() && s.Year == staff.AssignedYear);

        if (!string.IsNullOrWhiteSpace(staff.AssignedClass))
        {
            query = query.Where(s => s.ClassSection != null && s.ClassSection.ToUpper() == staff.AssignedClass.ToUpper());
        }

        return query;
    }
}
