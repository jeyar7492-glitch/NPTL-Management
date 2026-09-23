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
    private readonly IStaffRepository _staffRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IStaffAuthorizationService _staffAuthorizationService;
    private readonly ILogger<StaffController> _logger;

    public StaffController(
        IStaffRepository staffRepository,
        IStudentRepository studentRepository,
        IStaffAuthorizationService staffAuthorizationService,
        ILogger<StaffController> logger)
    {
        _staffRepository = staffRepository;
        _studentRepository = studentRepository;
        _staffAuthorizationService = staffAuthorizationService;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentStaff(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var staff = await _staffRepository.GetByUserIdAsync(userId, cancellationToken);
        if (staff == null)
            return NotFound(ApiResponse<object>.FailureResponse("Staff profile record not found."));

        var profile = new StaffProfileResponse
        {
            StaffId = staff.StaffId,
            StaffName = staff.StaffName,
            StaffIdentifier = staff.StaffIdentifier,
            Department = staff.Department,
            AssignedYear = staff.AssignedYear,
            AssignedClass = staff.AssignedClass
        };

        return Ok(ApiResponse<StaffProfileResponse>.SuccessResponse(profile, "Staff profile retrieved successfully."));
    }

    [HttpGet("students")]
    public async Task<IActionResult> GetAssignedStudents(
        [FromQuery] string? search,
        [FromQuery] string? classSection,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        var staff = await _staffRepository.GetByUserIdAsync(userId, cancellationToken);
        if (staff == null)
            return NotFound(ApiResponse<object>.FailureResponse("Staff profile record not found."));

        // Effective filter: client classSection can ONLY narrow within staff.AssignedClass
        string? effectiveClass = staff.AssignedClass;
        if (!string.IsNullOrWhiteSpace(classSection))
        {
            if (!string.IsNullOrWhiteSpace(staff.AssignedClass) &&
                !string.Equals(staff.AssignedClass, classSection, StringComparison.OrdinalIgnoreCase))
            {
                // Client requested class outside staff assignment -> empty list (cannot expand scope)
                return Ok(ApiResponse<List<StudentProfileResponse>>.SuccessResponse(new List<StudentProfileResponse>(), "Students retrieved."));
            }
            effectiveClass = classSection;
        }

        // Fetch students strictly within staff's authorized department and assigned year
        var students = await _studentRepository.GetAssignedStudentsAsync(
            staff.Department,
            staff.AssignedYear,
            effectiveClass,
            cancellationToken);

        // In-scope search filtering
        if (!string.IsNullOrWhiteSpace(search))
        {
            var sTerm = search.Trim().ToLowerInvariant();
            students = students.Where(s =>
                s.Name.ToLowerInvariant().Contains(sTerm) ||
                s.RegisterNumber.ToLowerInvariant().Contains(sTerm)
            ).ToList();
        }

        var result = students.Select(s => new StudentProfileResponse
        {
            StudentId = s.StudentId,
            Name = s.Name,
            RegisterNumber = s.RegisterNumber,
            Department = s.Department,
            ClassSection = s.ClassSection,
            Year = s.Year,
            Batch = s.Batch,
            Email = s.Email,
            Phone = s.Phone
        }).ToList();

        return Ok(ApiResponse<List<StudentProfileResponse>>.SuccessResponse(result, $"Retrieved {result.Count} authorized student(s)."));
    }

    [HttpGet("students/{id:guid}")]
    public async Task<IActionResult> GetStudentById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid staff token context."));

        // Strict Server-Side Scope Authorization Check
        var isAuthorized = await _staffAuthorizationService.CanStaffAccessStudentAsync(userId, id, cancellationToken);
        if (!isAuthorized)
        {
            _logger.LogWarning("Staff user {UserId} attempted unauthorized access to student {StudentId}", userId, id);
            return Forbid();
        }

        var student = await _studentRepository.GetByIdAsync(id, cancellationToken);
        if (student == null)
            return NotFound(ApiResponse<object>.FailureResponse("Student not found."));

        var response = new StudentProfileResponse
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

        return Ok(ApiResponse<StudentProfileResponse>.SuccessResponse(response, "Student details retrieved."));
    }

    private Guid GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(idStr, out var id);
        return id;
    }
}
