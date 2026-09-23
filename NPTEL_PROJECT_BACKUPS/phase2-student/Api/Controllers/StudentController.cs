using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Student")]
public class StudentController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly IStudentCourseService _studentCourseService;
    private readonly IStudentNotificationService _studentNotificationService;
    private readonly ILogger<StudentController> _logger;

    public StudentController(
        IStudentRepository studentRepository,
        IStudentCourseService studentCourseService,
        IStudentNotificationService studentNotificationService,
        ILogger<StudentController> logger)
    {
        _studentRepository = studentRepository;
        _studentCourseService = studentCourseService;
        _studentNotificationService = studentNotificationService;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentStudent(CancellationToken cancellationToken)
    {
        var (userId, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var student = await _studentRepository.GetByIdAsync(studentId.Value, cancellationToken);
        if (student == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Student profile record not found."));
        }

        var profile = new StudentProfileResponse
        {
            StudentId = student.StudentId,
            Name = student.Name,
            RegisterNumber = student.RegisterNumber,
            Department = student.Department,
            ClassSection = student.ClassSection,
            Year = student.Year,
            Batch = student.Batch,
            Email = student.Email,
            Phone = student.Phone
        };

        return Ok(ApiResponse<StudentProfileResponse>.SuccessResponse(profile, "Profile retrieved successfully."));
    }

    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var (_, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var summary = await _studentCourseService.GetDashboardSummaryAsync(studentId.Value, cancellationToken);
        return Ok(ApiResponse<StudentDashboardSummaryDto>.SuccessResponse(summary, "Dashboard summary retrieved successfully."));
    }

    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses(CancellationToken cancellationToken)
    {
        var (_, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var courses = await _studentCourseService.GetStudentCoursesAsync(studentId.Value, cancellationToken);
        return Ok(ApiResponse<List<StudentCourseDto>>.SuccessResponse(courses, "Student courses retrieved successfully."));
    }

    [HttpGet("courses/{registrationId:guid}")]
    public async Task<IActionResult> GetCourseDetails([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var (_, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var details = await _studentCourseService.GetCourseDetailsAsync(studentId.Value, registrationId, cancellationToken);
        if (details == null)
        {
            // Strict ownership: 404 Not Found if non-existent or not owned
            return NotFound(ApiResponse<object>.FailureResponse("Course registration not found."));
        }

        return Ok(ApiResponse<StudentCourseDetailsDto>.SuccessResponse(details, "Course details retrieved successfully."));
    }

    [HttpGet("courses/{registrationId:guid}/timeline")]
    public async Task<IActionResult> GetCourseTimeline([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var (_, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var timeline = await _studentCourseService.GetCourseTimelineAsync(studentId.Value, registrationId, cancellationToken);
        if (timeline == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Course timeline not found."));
        }

        return Ok(ApiResponse<List<StudentTimelineItemDto>>.SuccessResponse(timeline, "Timeline retrieved successfully."));
    }

    [HttpGet("courses/{registrationId:guid}/exam")]
    public async Task<IActionResult> GetCourseExam([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var (_, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var exam = await _studentCourseService.GetCourseExamStatusAsync(studentId.Value, registrationId, cancellationToken);
        if (exam == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Exam details not found."));
        }

        return Ok(ApiResponse<StudentExamDto>.SuccessResponse(exam, "Exam details retrieved successfully."));
    }

    [HttpGet("courses/{registrationId:guid}/certificate")]
    public async Task<IActionResult> GetCourseCertificate([FromRoute] Guid registrationId, CancellationToken cancellationToken)
    {
        var (_, studentId) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (studentId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid student token context."));
        }

        var certificate = await _studentCourseService.GetCourseCertificateStatusAsync(studentId.Value, registrationId, cancellationToken);
        if (certificate == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Certificate details not found."));
        }

        return Ok(ApiResponse<StudentCertificateDto>.SuccessResponse(certificate, "Certificate details retrieved successfully."));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var (userId, _) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid user token context."));
        }

        var notifications = await _studentNotificationService.GetStudentNotificationsAsync(userId.Value, cancellationToken);
        return Ok(ApiResponse<List<StudentNotificationDto>>.SuccessResponse(notifications, "Notifications retrieved successfully."));
    }

    [HttpPost("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationAsRead([FromRoute] Guid notificationId, CancellationToken cancellationToken)
    {
        var (userId, _) = await GetAuthenticatedStudentContextAsync(cancellationToken);
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid user token context."));
        }

        var success = await _studentNotificationService.MarkNotificationAsReadAsync(userId.Value, notificationId, cancellationToken);
        if (!success)
        {
            // Strict ownership: 404 if notification doesn't exist or doesn't belong to authenticated user
            return NotFound(ApiResponse<object>.FailureResponse("Notification not found or access denied."));
        }

        return Ok(ApiResponse<object>.SuccessResponse(new { notificationId, isRead = true }, "Notification marked as read."));
    }

    private async Task<(Guid? userId, Guid? studentId)> GetAuthenticatedStudentContextAsync(CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return (null, null);
        }

        var studentIdStr = User.FindFirst("student_id")?.Value
                           ?? User.FindFirst("role_id")?.Value;

        if (Guid.TryParse(studentIdStr, out var parsedStudentId))
        {
            return (userId, parsedStudentId);
        }

        // Fallback: look up student by verified User ID
        var student = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
        return (userId, student?.StudentId);
    }
}
