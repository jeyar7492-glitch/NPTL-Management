using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminRepository _adminRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly ICertificateRepository _certificateRepository;
    private readonly ApplicationDbContext _context;

    // Phase 4 Services
    private readonly IAdminStudentService _adminStudentService;
    private readonly IAdminStaffService _adminStaffService;
    private readonly IAdminCourseService _adminCourseService;
    private readonly IAdminRegistrationService _adminRegistrationService;
    private readonly IAdminExamService _adminExamService;
    private readonly IAdminCertificateService _adminCertificateService;
    private readonly IAdminNotificationService _adminNotificationService;
    private readonly IAdminReportService _adminReportService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminRepository adminRepository,
        IStudentRepository studentRepository,
        IStaffRepository staffRepository,
        ICourseRepository courseRepository,
        IRegistrationRepository registrationRepository,
        ICertificateRepository certificateRepository,
        ApplicationDbContext context,
        IAdminStudentService adminStudentService,
        IAdminStaffService adminStaffService,
        IAdminCourseService adminCourseService,
        IAdminRegistrationService adminRegistrationService,
        IAdminExamService adminExamService,
        IAdminCertificateService adminCertificateService,
        IAdminNotificationService adminNotificationService,
        IAdminReportService adminReportService,
        IAuditLogService auditLogService,
        ILogger<AdminController> logger)
    {
        _adminRepository = adminRepository;
        _studentRepository = studentRepository;
        _staffRepository = staffRepository;
        _courseRepository = courseRepository;
        _registrationRepository = registrationRepository;
        _certificateRepository = certificateRepository;
        _context = context;
        _adminStudentService = adminStudentService;
        _adminStaffService = adminStaffService;
        _adminCourseService = adminCourseService;
        _adminRegistrationService = adminRegistrationService;
        _adminExamService = adminExamService;
        _adminCertificateService = adminCertificateService;
        _adminNotificationService = adminNotificationService;
        _adminReportService = adminReportService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    #region Helper Methods
    private Guid GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    private string GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
    #endregion

    #region 1. Dashboard Metrics
    [HttpGet("me")]
    public async Task<IActionResult> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid admin token context."));
        }

        var admin = await _adminRepository.GetByUserIdAsync(userId, cancellationToken);
        if (admin == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Admin profile record not found."));
        }

        var totalStudents = await _studentRepository.GetCountAsync(cancellationToken);
        var totalStaff = await _staffRepository.GetCountAsync(cancellationToken);
        var totalCourses = await _courseRepository.GetCountAsync(cancellationToken);
        var totalRegistrations = await _registrationRepository.GetCountAsync(cancellationToken);
        var totalCertificates = await _certificateRepository.GetCountAsync(cancellationToken);

        var pendingCertificates = await _context.Certificates
            .CountAsync(c => c.VerifiedStatus == Core.Enums.CertificateStatus.Pending || 
                             c.VerifiedStatus == Core.Enums.CertificateStatus.Submitted ||
                             c.VerifiedStatus == Core.Enums.CertificateStatus.UnderVerification, cancellationToken);

        var verifiedCertificates = await _context.Certificates
            .CountAsync(c => c.VerifiedStatus == Core.Enums.CertificateStatus.Verified || 
                             c.VerifiedStatus == Core.Enums.CertificateStatus.Received, cancellationToken);

        var completedExams = await _context.ExamStatuses
            .CountAsync(e => e.Status == "Completed", cancellationToken);

        var response = new AdminProfileResponse
        {
            AdminId = admin.AdminId,
            AdminIdentifier = admin.AdminIdentifier,
            TotalStudents = totalStudents,
            TotalStaff = totalStaff,
            TotalCourses = totalCourses,
            TotalRegistrations = totalRegistrations,
            TotalCertificates = totalCertificates,
            PendingCertificates = pendingCertificates,
            VerifiedCertificates = verifiedCertificates,
            CompletedExams = completedExams
        };

        return Ok(ApiResponse<AdminProfileResponse>.SuccessResponse(response, "Admin dashboard statistics retrieved."));
    }

    [HttpGet("dashboard")]
    [HttpGet("dashboard-metrics")]
    public async Task<IActionResult> GetDashboardDetailed(CancellationToken cancellationToken)
    {
        var totalStudents = await _studentRepository.GetCountAsync(cancellationToken);
        var totalStaff = await _staffRepository.GetCountAsync(cancellationToken);
        var totalCourses = await _courseRepository.GetCountAsync(cancellationToken);
        var totalRegistrations = await _registrationRepository.GetCountAsync(cancellationToken);
        var totalCertificates = await _certificateRepository.GetCountAsync(cancellationToken);

        var pendingCertificates = await _context.Certificates
            .CountAsync(c => c.VerifiedStatus == Core.Enums.CertificateStatus.Pending || 
                             c.VerifiedStatus == Core.Enums.CertificateStatus.Submitted ||
                             c.VerifiedStatus == Core.Enums.CertificateStatus.UnderVerification, cancellationToken);

        var verifiedCertificates = await _context.Certificates
            .CountAsync(c => c.VerifiedStatus == Core.Enums.CertificateStatus.Verified || 
                             c.VerifiedStatus == Core.Enums.CertificateStatus.Received, cancellationToken);

        var completedExams = await _context.ExamStatuses
            .CountAsync(e => e.Status == "Completed", cancellationToken);

        var recentRegistrations = await _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .OrderByDescending(r => r.EnrollmentDate)
            .Take(5)
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
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        var recentAuditLogs = await _context.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(5)
            .Select(a => new AuditLogDto
            {
                LogId = a.LogId,
                UserId = a.UserId,
                Username = a.User != null ? a.User.Username : "System",
                Role = a.User != null ? a.User.Role.ToString() : "Admin",
                Action = a.Action,
                Details = a.Details,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp
            })
            .ToListAsync(cancellationToken);

        var metrics = new AdminDashboardMetricsDto
        {
            TotalStudents = totalStudents,
            TotalStaff = totalStaff,
            TotalCourses = totalCourses,
            TotalRegistrations = totalRegistrations,
            TotalCertificates = totalCertificates,
            PendingCertificates = pendingCertificates,
            VerifiedCertificates = verifiedCertificates,
            CompletedExams = completedExams,
            RecentRegistrations = recentRegistrations,
            RecentAuditLogs = recentAuditLogs
        };

        return Ok(ApiResponse<AdminDashboardMetricsDto>.SuccessResponse(metrics, "Detailed admin dashboard metrics retrieved."));
    }
    #endregion

    #region 2. Student Management
    [HttpGet("students")]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? search,
        [FromQuery] string? department,
        [FromQuery] int? year,
        [FromQuery] string? classSection,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminStudentService.GetStudentsAsync(search, department, year, classSection, isActive, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminStudentListDto>>.SuccessResponse(result, "Students retrieved successfully."));
    }

    [HttpGet("students/{studentId:guid}")]
    public async Task<IActionResult> GetStudentById([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var student = await _adminStudentService.GetStudentByIdAsync(studentId, cancellationToken);
        if (student == null)
            return NotFound(ApiResponse<object>.FailureResponse("Student not found."));

        return Ok(ApiResponse<AdminStudentDetailDto>.SuccessResponse(student, "Student details retrieved."));
    }

    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _adminStudentService.CreateStudentAsync(dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return StatusCode(201, ApiResponse<AdminStudentDetailDto>.SuccessResponse(created, "Student created successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPut("students/{studentId:guid}")]
    public async Task<IActionResult> UpdateStudent([FromRoute] Guid studentId, [FromBody] UpdateStudentDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminStudentService.UpdateStudentAsync(studentId, dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return Ok(ApiResponse<AdminStudentDetailDto>.SuccessResponse(updated, "Student updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPatch("students/{studentId:guid}/status")]
    public async Task<IActionResult> UpdateStudentStatus([FromRoute] Guid studentId, [FromBody] UpdateStatusDto dto, CancellationToken cancellationToken)
    {
        var success = await _adminStudentService.UpdateStudentStatusAsync(studentId, dto.IsActive, GetCurrentUserId(), GetClientIp(), cancellationToken);
        if (!success)
            return NotFound(ApiResponse<object>.FailureResponse("Student not found."));

        return Ok(ApiResponse<object>.SuccessResponse(new { studentId, isActive = dto.IsActive }, "Student status updated successfully."));
    }
    #endregion

    #region 3. Staff Management
    [HttpGet("staff")]
    public async Task<IActionResult> GetStaff(
        [FromQuery] string? search,
        [FromQuery] string? department,
        [FromQuery] int? assignedYear,
        [FromQuery] string? assignedClass,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminStaffService.GetStaffMembersAsync(search, department, assignedYear, assignedClass, isActive, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminStaffListDto>>.SuccessResponse(result, "Staff members retrieved successfully."));
    }

    [HttpGet("staff/{staffId:guid}")]
    public async Task<IActionResult> GetStaffById([FromRoute] Guid staffId, CancellationToken cancellationToken)
    {
        var staff = await _adminStaffService.GetStaffByIdAsync(staffId, cancellationToken);
        if (staff == null)
            return NotFound(ApiResponse<object>.FailureResponse("Staff member not found."));

        return Ok(ApiResponse<AdminStaffDetailDto>.SuccessResponse(staff, "Staff details retrieved."));
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _adminStaffService.CreateStaffAsync(dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return StatusCode(201, ApiResponse<AdminStaffDetailDto>.SuccessResponse(created, "Staff member created successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPut("staff/{staffId:guid}")]
    public async Task<IActionResult> UpdateStaff([FromRoute] Guid staffId, [FromBody] UpdateStaffDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminStaffService.UpdateStaffAsync(staffId, dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return Ok(ApiResponse<AdminStaffDetailDto>.SuccessResponse(updated, "Staff member updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPatch("staff/{staffId:guid}/status")]
    public async Task<IActionResult> UpdateStaffStatus([FromRoute] Guid staffId, [FromBody] UpdateStatusDto dto, CancellationToken cancellationToken)
    {
        var success = await _adminStaffService.UpdateStaffStatusAsync(staffId, dto.IsActive, GetCurrentUserId(), GetClientIp(), cancellationToken);
        if (!success)
            return NotFound(ApiResponse<object>.FailureResponse("Staff member not found."));

        return Ok(ApiResponse<object>.SuccessResponse(new { staffId, isActive = dto.IsActive }, "Staff status updated successfully."));
    }

    [HttpPost("staff/{staffId:guid}/reset-password")]
    [HttpPatch("staff/{staffId:guid}/password")]
    public async Task<IActionResult> ResetStaffPassword([FromRoute] Guid staffId, [FromBody] ResetStaffPasswordDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _adminStaffService.ResetStaffPasswordAsync(staffId, dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            if (!success)
                return NotFound(ApiResponse<object>.FailureResponse("Staff member not found."));

            return Ok(ApiResponse<object>.SuccessResponse(new { staffId }, "Staff password reset successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }
    #endregion

    #region 4. Course Management
    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminCourseService.GetCoursesAsync(search, status, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminCourseDto>>.SuccessResponse(result, "Courses retrieved successfully."));
    }

    [HttpGet("courses/{courseId:guid}")]
    public async Task<IActionResult> GetCourseById([FromRoute] Guid courseId, CancellationToken cancellationToken)
    {
        var course = await _adminCourseService.GetCourseByIdAsync(courseId, cancellationToken);
        if (course == null)
            return NotFound(ApiResponse<object>.FailureResponse("Course not found."));

        return Ok(ApiResponse<AdminCourseDto>.SuccessResponse(course, "Course details retrieved."));
    }

    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _adminCourseService.CreateCourseAsync(dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return StatusCode(201, ApiResponse<AdminCourseDto>.SuccessResponse(created, "Course created successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPut("courses/{courseId:guid}")]
    public async Task<IActionResult> UpdateCourse([FromRoute] Guid courseId, [FromBody] UpdateCourseDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminCourseService.UpdateCourseAsync(courseId, dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return Ok(ApiResponse<AdminCourseDto>.SuccessResponse(updated, "Course updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPatch("courses/{courseId:guid}/status")]
    public async Task<IActionResult> UpdateCourseStatus([FromRoute] Guid courseId, [FromBody] UpdateCourseStatusDto dto, CancellationToken cancellationToken)
    {
        var success = await _adminCourseService.UpdateCourseStatusAsync(courseId, dto.Status, GetCurrentUserId(), GetClientIp(), cancellationToken);
        if (!success)
            return NotFound(ApiResponse<object>.FailureResponse("Course not found."));

        return Ok(ApiResponse<object>.SuccessResponse(new { courseId, status = dto.Status }, "Course status updated successfully."));
    }
    #endregion

    #region 5. Registration Management
    [HttpGet("registrations")]
    public async Task<IActionResult> GetRegistrations(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? courseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminRegistrationService.GetRegistrationsAsync(search, status, studentId, courseId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminRegistrationDto>>.SuccessResponse(result, "Registrations retrieved successfully."));
    }

    [HttpGet("registrations/{registrationId:guid}")]
    public async Task<IActionResult> GetRegistrationById([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var reg = await _adminRegistrationService.GetRegistrationByIdAsync(registrationId, cancellationToken);
        if (reg == null)
            return NotFound(ApiResponse<object>.FailureResponse("Registration not found."));

        return Ok(ApiResponse<AdminRegistrationDto>.SuccessResponse(reg, "Registration retrieved."));
    }

    [HttpPost("registrations")]
    public async Task<IActionResult> CreateRegistration([FromBody] CreateRegistrationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _adminRegistrationService.CreateRegistrationAsync(dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return StatusCode(201, ApiResponse<AdminRegistrationDto>.SuccessResponse(created, "Student enrolled successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpPatch("registrations/{registrationId:guid}/status")]
    public async Task<IActionResult> UpdateRegistrationStatus(
        [FromRoute] Guid registrationId, 
        [FromBody] UpdateRegistrationDto dto, 
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminRegistrationService.UpdateRegistrationStatusAsync(registrationId, dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return Ok(ApiResponse<AdminRegistrationDto>.SuccessResponse(updated, "Registration status updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }
    #endregion

    #region 6. Exam Management
    [HttpGet("exams")]
    public async Task<IActionResult> GetExams(
        [FromQuery] string? search,
        [FromQuery] string? examStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminExamService.GetExamsAsync(search, examStatus, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminExamDto>>.SuccessResponse(result, "Exams retrieved successfully."));
    }

    [HttpGet("exams/registration/{registrationId:guid}")]
    public async Task<IActionResult> GetExamByRegistrationId([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var exam = await _adminExamService.GetExamByRegistrationIdAsync(registrationId, cancellationToken);
        if (exam == null)
            return NotFound(ApiResponse<object>.FailureResponse("Exam details not found for this registration."));

        return Ok(ApiResponse<AdminExamDto>.SuccessResponse(exam, "Exam details retrieved."));
    }

    [HttpPut("exams/registration/{registrationId:guid}")]
    public async Task<IActionResult> UpdateExam(
        [FromRoute] Guid registrationId,
        [FromBody] UpdateAdminExamDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminExamService.UpdateExamAsync(registrationId, dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return Ok(ApiResponse<AdminExamDto>.SuccessResponse(updated, "Exam details updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }
    #endregion

    #region 7. Certificate Management
    [HttpGet("certificates")]
    public async Task<IActionResult> GetCertificates(
        [FromQuery] string? search,
        [FromQuery] string? verifiedStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminCertificateService.GetCertificatesAsync(search, verifiedStatus, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminCertificateDto>>.SuccessResponse(result, "Certificates retrieved successfully."));
    }

    [HttpGet("certificates/registration/{registrationId:guid}")]
    public async Task<IActionResult> GetCertificateByRegistrationId([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var cert = await _adminCertificateService.GetCertificateByRegistrationIdAsync(registrationId, cancellationToken);
        if (cert == null)
            return NotFound(ApiResponse<object>.FailureResponse("Certificate record not found for this registration."));

        return Ok(ApiResponse<AdminCertificateDto>.SuccessResponse(cert, "Certificate retrieved."));
    }

    [HttpPost("certificates/registration/{registrationId:guid}/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCertificate(
        [FromRoute] Guid registrationId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.FailureResponse("No file provided."));
        }

        try
        {
            using var stream = file.OpenReadStream();
            var uploaded = await _adminCertificateService.UploadCertificateAsync(
                registrationId, 
                stream, 
                file.FileName, 
                file.ContentType, 
                file.Length, 
                GetCurrentUserId(), 
                GetClientIp(), 
                cancellationToken);

            return Ok(ApiResponse<AdminCertificateDto>.SuccessResponse(uploaded, "Certificate uploaded successfully to private cloud storage."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading certificate for registration {RegistrationId}", registrationId);
            return StatusCode(500, ApiResponse<object>.FailureResponse($"Upload failed: {ex.Message}"));
        }
    }

    [HttpPatch("certificates/registration/{registrationId:guid}/status")]
    public async Task<IActionResult> UpdateCertificateStatus(
        [FromRoute] Guid registrationId,
        [FromBody] UpdateCertificateStatusDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminCertificateService.UpdateCertificateStatusAsync(
                registrationId, 
                dto, 
                GetCurrentUserId(), 
                GetClientIp(), 
                cancellationToken);

            return Ok(ApiResponse<AdminCertificateDto>.SuccessResponse(updated, "Certificate status updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpGet("certificates/{certificateId:guid}/access")]
    public async Task<IActionResult> GetCertificateAccess([FromRoute] Guid certificateId, CancellationToken cancellationToken)
    {
        try
        {
            var access = await _adminCertificateService.GetCertificateAccessAsync(
                certificateId, 
                GetCurrentUserId(), 
                "Admin", 
                cancellationToken);

            return Ok(ApiResponse<CertificateAccessResponseDto>.SuccessResponse(access, "Signed certificate access URL generated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.FailureResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }
    #endregion

    #region 8. Notifications Management
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminNotificationService.GetNotificationsAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminNotificationDto>>.SuccessResponse(result, "Notifications retrieved successfully."));
    }

    [HttpPost("notifications")]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _adminNotificationService.CreateNotificationAsync(dto, GetCurrentUserId(), GetClientIp(), cancellationToken);
            return StatusCode(201, ApiResponse<object>.SuccessResponse(new { dispatchedCount = count }, $"Notification dispatched to {count} recipient(s)."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(ex.Message));
        }
    }

    [HttpDelete("notifications/{notificationId:guid}")]
    public async Task<IActionResult> DeleteNotification([FromRoute] Guid notificationId, CancellationToken cancellationToken)
    {
        var success = await _adminNotificationService.DeleteNotificationAsync(notificationId, GetCurrentUserId(), GetClientIp(), cancellationToken);
        if (!success)
            return NotFound(ApiResponse<object>.FailureResponse("Notification not found."));

        return Ok(ApiResponse<object>.SuccessResponse(new { notificationId }, "Notification deleted successfully."));
    }

    [HttpPost("notifications/trigger-rules")]
    public async Task<IActionResult> TriggerAutomatedNotificationRules(CancellationToken cancellationToken)
    {
        var count = await _adminNotificationService.TriggerAutomatedRulesAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { generatedCount = count }, $"Automated notification engine generated {count} notification(s)."));
    }
    #endregion

    #region 9. Report Management & Exports
    [HttpPost("reports/preview")]
    public async Task<IActionResult> GetReportPreview([FromBody] AdminReportFilterDto filter, CancellationToken cancellationToken)
    {
        var preview = await _adminReportService.GetReportPreviewAsync(filter, cancellationToken);
        return Ok(ApiResponse<AdminReportPreviewDto>.SuccessResponse(preview, "Report preview generated."));
    }

    [HttpPost("reports/export-pdf")]
    public async Task<IActionResult> ExportReportPdf([FromBody] AdminReportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _adminReportService.GeneratePdfReportAsync(filter, cancellationToken);
        var filename = $"NPTEL_{filter.ReportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpPost("reports/export-xlsx")]
    public async Task<IActionResult> ExportReportXlsx([FromBody] AdminReportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _adminReportService.GenerateXlsxReportAsync(filter, cancellationToken);
        var filename = $"NPTEL_{filter.ReportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    [HttpPost("reports/export-csv")]
    public async Task<IActionResult> ExportReportCsv([FromBody] AdminReportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _adminReportService.GenerateCsvReportAsync(filter, cancellationToken);
        var filename = $"NPTEL_{filter.ReportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", filename);
    }
    #endregion

    #region 10. Audit Logs
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _auditLogService.GetAuditLogsAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.SuccessResponse(result, "Audit logs retrieved successfully."));
    }
    #endregion
}
