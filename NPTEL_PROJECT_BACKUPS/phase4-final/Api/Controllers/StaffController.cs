using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Staff")]
public class StaffController : ControllerBase
{
    private readonly IStaffStudentService _staffStudentService;
    private readonly IStaffReportService _staffReportService;
    private readonly IAdminCertificateService _adminCertificateService;
    private readonly ILogger<StaffController> _logger;

    public StaffController(
        IStaffStudentService staffStudentService,
        IStaffReportService staffReportService,
        IAdminCertificateService adminCertificateService,
        ILogger<StaffController> logger)
    {
        _staffStudentService = staffStudentService;
        _staffReportService = staffReportService;
        _adminCertificateService = adminCertificateService;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentStaff(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var profile = await _staffStudentService.GetStaffProfileAsync(userId, cancellationToken);
        if (profile == null)
            return NotFound(ApiResponse<object>.FailureResponse("Staff profile record not found."));

        return Ok(ApiResponse<StaffProfileResponse>.SuccessResponse(profile, "Staff profile retrieved successfully."));
    }

    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var summary = await _staffStudentService.GetDashboardSummaryAsync(userId, cancellationToken);
        return Ok(ApiResponse<StaffDashboardSummaryDto>.SuccessResponse(summary, "Staff dashboard summary retrieved successfully."));
    }

    [HttpGet("students")]
    public async Task<IActionResult> GetAssignedStudents(
        [FromQuery] StaffStudentFilterDto filter,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var result = await _staffStudentService.GetAssignedStudentsAsync(userId, filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<StaffStudentListDto>>.SuccessResponse(result, $"Retrieved {result.Items.Count} authorized student(s)."));
    }

    [HttpGet("students/{studentId:guid}")]
    public async Task<IActionResult> GetStudentById([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var student = await _staffStudentService.GetStudentDetailsAsync(userId, studentId, cancellationToken);
        if (student == null)
        {
            _logger.LogWarning("Staff user {UserId} requested student {StudentId} which is outside authorized scope or not found.", userId, studentId);
            return NotFound(ApiResponse<object>.FailureResponse("Student not found or access denied."));
        }

        return Ok(ApiResponse<StaffStudentDetailsDto>.SuccessResponse(student, "Student details retrieved successfully."));
    }

    [HttpGet("students/{studentId:guid}/courses")]
    public async Task<IActionResult> GetStudentCourses([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var courses = await _staffStudentService.GetStudentCoursesAsync(userId, studentId, cancellationToken);
        if (courses == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Student not found or access denied."));
        }

        return Ok(ApiResponse<List<StaffStudentCourseDto>>.SuccessResponse(courses, "Student courses retrieved successfully."));
    }

    [HttpGet("students/{studentId:guid}/timeline")]
    public async Task<IActionResult> GetStudentTimeline(
        [FromRoute] Guid studentId,
        [FromQuery] Guid? registrationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var timeline = await _staffStudentService.GetStudentTimelineAsync(userId, studentId, registrationId, cancellationToken);
        if (timeline == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Student not found or access denied."));
        }

        return Ok(ApiResponse<List<StudentTimelineItemDto>>.SuccessResponse(timeline, "Timeline retrieved successfully."));
    }

    [HttpGet("students/{studentId:guid}/exam")]
    public async Task<IActionResult> GetStudentExam(
        [FromRoute] Guid studentId,
        [FromQuery] Guid? registrationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var exam = await _staffStudentService.GetStudentExamStatusAsync(userId, studentId, registrationId, cancellationToken);
        if (exam == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Student not found or access denied."));
        }

        return Ok(ApiResponse<StudentExamDto>.SuccessResponse(exam, "Exam details retrieved successfully."));
    }

    [HttpGet("students/{studentId:guid}/certificate")]
    public async Task<IActionResult> GetStudentCertificate(
        [FromRoute] Guid studentId,
        [FromQuery] Guid? registrationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var certificate = await _staffStudentService.GetStudentCertificateStatusAsync(userId, studentId, registrationId, cancellationToken);
        if (certificate == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Student not found or access denied."));
        }

        return Ok(ApiResponse<StudentCertificateDto>.SuccessResponse(certificate, "Certificate details retrieved successfully."));
    }

    [HttpGet("reports/preview")]
    public async Task<IActionResult> GetReportPreview(
        [FromQuery] string reportType,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        if (string.IsNullOrWhiteSpace(reportType))
        {
            reportType = "student-registration";
        }

        var report = await _staffReportService.GetReportPreviewAsync(userId, reportType, cancellationToken);
        if (report == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Staff context not found."));
        }

        return Ok(ApiResponse<StaffReportPreviewDto>.SuccessResponse(report, "Report preview generated successfully."));
    }

    [HttpGet("certificates/{certificateId:guid}/access")]
    public async Task<IActionResult> GetCertificateAccess([FromRoute] Guid certificateId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));
        }

        try
        {
            var access = await _adminCertificateService.GetCertificateAccessAsync(
                certificateId, 
                userId, 
                "Staff", 
                cancellationToken);

            return Ok(ApiResponse<CertificateAccessResponseDto>.SuccessResponse(access, "Signed certificate access URL generated."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Certificate not found."));
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

    private Guid GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(idStr, out var id);
        return id;
    }
}
