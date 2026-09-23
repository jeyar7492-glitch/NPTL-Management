using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;

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
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminRepository adminRepository,
        IStudentRepository studentRepository,
        IStaffRepository staffRepository,
        ICourseRepository courseRepository,
        IRegistrationRepository registrationRepository,
        ICertificateRepository certificateRepository,
        ILogger<AdminController> logger)
    {
        _adminRepository = adminRepository;
        _studentRepository = studentRepository;
        _staffRepository = staffRepository;
        _courseRepository = courseRepository;
        _registrationRepository = registrationRepository;
        _certificateRepository = certificateRepository;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(idStr, out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid admin token context."));
        }

        var admin = await _adminRepository.GetByUserIdAsync(userId, cancellationToken);
        if (admin == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Admin profile record not found."));
        }

        // Real counts queried live from the database
        var totalStudents = await _studentRepository.GetCountAsync(cancellationToken);
        var totalStaff = await _staffRepository.GetCountAsync(cancellationToken);
        var totalCourses = await _courseRepository.GetCountAsync(cancellationToken);
        var totalRegistrations = await _registrationRepository.GetCountAsync(cancellationToken);
        var totalCertificates = await _certificateRepository.GetCountAsync(cancellationToken);

        var response = new AdminProfileResponse
        {
            AdminId = admin.AdminId,
            AdminIdentifier = admin.AdminIdentifier,
            TotalStudents = totalStudents,
            TotalStaff = totalStaff,
            TotalCourses = totalCourses,
            TotalRegistrations = totalRegistrations,
            TotalCertificates = totalCertificates
        };

        return Ok(ApiResponse<AdminProfileResponse>.SuccessResponse(response, "Admin dashboard statistics retrieved."));
    }
}
