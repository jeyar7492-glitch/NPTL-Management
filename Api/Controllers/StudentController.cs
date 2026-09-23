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
    private readonly ILogger<StudentController> _logger;

    public StudentController(IStudentRepository studentRepository, ILogger<StudentController> logger)
    {
        _studentRepository = studentRepository;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentStudent(CancellationToken cancellationToken)
    {
        // Derive student identity strictly from authenticated JWT claims
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid user token context."));
        }

        var student = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
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
}
