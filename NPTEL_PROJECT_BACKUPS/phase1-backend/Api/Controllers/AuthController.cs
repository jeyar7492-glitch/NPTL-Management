using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginRateLimiter _rateLimiter;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IStaffRepository staffRepository,
        IAdminRepository adminRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILoginRateLimiter rateLimiter,
        IAuditLogRepository auditLogRepository,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _staffRepository = staffRepository;
        _adminRepository = adminRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _rateLimiter = rateLimiter;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    [HttpPost("student/login")]
    [AllowAnonymous]
    public async Task<IActionResult> StudentLogin([FromBody] StudentLoginDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.FailureResponse("Invalid request payload."));

        var rateLimitKey = $"student:{request.RegisterNumber.Trim().ToUpper()}:{GetClientIp()}";
        if (!_rateLimiter.IsAllowed(rateLimitKey))
        {
            var remaining = _rateLimiter.GetRemainingLockoutTime(rateLimitKey);
            return StatusCode(429, ApiResponse<object>.FailureResponse(
                $"Too many failed attempts. Please try again in {Math.Ceiling(remaining?.TotalMinutes ?? 5)} minute(s)."));
        }

        var student = await _studentRepository.GetByRegisterNumberAsync(request.RegisterNumber.Trim(), cancellationToken);
        if (student == null || student.User == null || !student.User.IsActive || student.User.Role != UserRole.Student)
        {
            _rateLimiter.RecordFailedAttempt(rateLimitKey);
            await LogAuditAsync(null, "StudentLoginFailed", $"Failed login attempt for register number: {request.RegisterNumber}", cancellationToken);
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid credentials."));
        }

        if (!_passwordHasher.VerifyPassword(request.Password, student.User.PasswordHash))
        {
            _rateLimiter.RecordFailedAttempt(rateLimitKey);
            await LogAuditAsync(student.UserId, "StudentLoginFailed", "Password verification failed", cancellationToken);
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid credentials."));
        }

        _rateLimiter.ResetAttempts(rateLimitKey);

        var token = _jwtTokenService.GenerateToken(
            student.User,
            student.RegisterNumber,
            student.Name,
            roleSpecificId: student.StudentId,
            department: student.Department);

        await LogAuditAsync(student.UserId, "StudentLoginSuccess", "Student logged in successfully", cancellationToken);

        return Ok(ApiResponse<AuthSuccessResponse>.SuccessResponse(new AuthSuccessResponse
        {
            AccessToken = token,
            ExpiresAt = _jwtTokenService.GetTokenExpiration(),
            Role = UserRole.Student.ToString(),
            Identifier = student.RegisterNumber,
            Name = student.Name
        }, "Login successful."));
    }

    [HttpPost("staff/login")]
    [AllowAnonymous]
    public async Task<IActionResult> StaffLogin([FromBody] StaffLoginDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.FailureResponse("Invalid request payload."));

        var rateLimitKey = $"staff:{request.StaffId.Trim().ToUpper()}:{GetClientIp()}";
        if (!_rateLimiter.IsAllowed(rateLimitKey))
        {
            var remaining = _rateLimiter.GetRemainingLockoutTime(rateLimitKey);
            return StatusCode(429, ApiResponse<object>.FailureResponse(
                $"Too many failed attempts. Please try again in {Math.Ceiling(remaining?.TotalMinutes ?? 5)} minute(s)."));
        }

        var staff = await _staffRepository.GetByIdentifierAsync(request.StaffId.Trim(), cancellationToken);
        if (staff == null || staff.User == null || !staff.User.IsActive || staff.User.Role != UserRole.Staff)
        {
            _rateLimiter.RecordFailedAttempt(rateLimitKey);
            await LogAuditAsync(null, "StaffLoginFailed", $"Failed login attempt for staff identifier: {request.StaffId}", cancellationToken);
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid credentials."));
        }

        if (!_passwordHasher.VerifyPassword(request.Password, staff.User.PasswordHash))
        {
            _rateLimiter.RecordFailedAttempt(rateLimitKey);
            await LogAuditAsync(staff.UserId, "StaffLoginFailed", "Password verification failed", cancellationToken);
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid credentials."));
        }

        _rateLimiter.ResetAttempts(rateLimitKey);

        var token = _jwtTokenService.GenerateToken(
            staff.User,
            staff.StaffIdentifier,
            staff.StaffName,
            roleSpecificId: staff.StaffId,
            department: staff.Department,
            assignedYear: staff.AssignedYear,
            assignedClass: staff.AssignedClass);

        await LogAuditAsync(staff.UserId, "StaffLoginSuccess", "Staff logged in successfully", cancellationToken);

        return Ok(ApiResponse<AuthSuccessResponse>.SuccessResponse(new AuthSuccessResponse
        {
            AccessToken = token,
            ExpiresAt = _jwtTokenService.GetTokenExpiration(),
            Role = UserRole.Staff.ToString(),
            Identifier = staff.StaffIdentifier,
            Name = staff.StaffName
        }, "Login successful."));
    }

    [HttpPost("admin/login")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.FailureResponse("Invalid request payload."));

        var rateLimitKey = $"admin:{request.AdminId.Trim().ToUpper()}:{GetClientIp()}";
        if (!_rateLimiter.IsAllowed(rateLimitKey))
        {
            var remaining = _rateLimiter.GetRemainingLockoutTime(rateLimitKey);
            return StatusCode(429, ApiResponse<object>.FailureResponse(
                $"Too many failed attempts. Please try again in {Math.Ceiling(remaining?.TotalMinutes ?? 5)} minute(s)."));
        }

        var admin = await _adminRepository.GetByIdentifierAsync(request.AdminId.Trim(), cancellationToken);
        if (admin == null || admin.User == null || !admin.User.IsActive || admin.User.Role != UserRole.Admin)
        {
            _rateLimiter.RecordFailedAttempt(rateLimitKey);
            await LogAuditAsync(null, "AdminLoginFailed", $"Failed login attempt for admin identifier: {request.AdminId}", cancellationToken);
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid credentials."));
        }

        if (!_passwordHasher.VerifyPassword(request.Password, admin.User.PasswordHash))
        {
            _rateLimiter.RecordFailedAttempt(rateLimitKey);
            await LogAuditAsync(admin.UserId, "AdminLoginFailed", "Password verification failed", cancellationToken);
            return Unauthorized(ApiResponse<object>.FailureResponse("Invalid credentials."));
        }

        _rateLimiter.ResetAttempts(rateLimitKey);

        var token = _jwtTokenService.GenerateToken(
            admin.User,
            admin.AdminIdentifier,
            admin.AdminIdentifier,
            roleSpecificId: admin.AdminId);

        await LogAuditAsync(admin.UserId, "AdminLoginSuccess", "Admin logged in successfully", cancellationToken);

        return Ok(ApiResponse<AuthSuccessResponse>.SuccessResponse(new AuthSuccessResponse
        {
            AccessToken = token,
            ExpiresAt = _jwtTokenService.GetTokenExpiration(),
            Role = UserRole.Admin.ToString(),
            Identifier = admin.AdminIdentifier,
            Name = admin.AdminIdentifier
        }, "Login successful."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdClaim, out var userId);

        await LogAuditAsync(userId != Guid.Empty ? userId : null, "Logout", "User requested logout", cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Logged out successfully."));
    }

    private string GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task LogAuditAsync(Guid? userId, string action, string details, CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                IpAddress = GetClientIp(),
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to write audit log: {Message}", ex.Message);
        }
    }
}
