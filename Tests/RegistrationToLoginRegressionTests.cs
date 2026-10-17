using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NPTELManagement.Api.Controllers;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Infrastructure.Authentication;
using NPTELManagement.Infrastructure.Data;
using NPTELManagement.Infrastructure.Repositories;
using NPTELManagement.Infrastructure.Services;
using Xunit;

namespace NPTELManagement.Tests;

public class RegistrationToLoginRegressionTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task StudentLogin_WithEmail_FindsStudentAndSucceeds()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();
        var studentRepo = new StudentRepository(context);
        var userRepo = new UserRepository(context);
        var staffRepo = new StaffRepository(context);
        var adminRepo = new AdminRepository(context);
        var rateLimiter = new LoginRateLimiter();
        var auditLogRepo = new AuditLogRepository(context);
        var auditLogService = new AuditLogService(context);
        var adminStudentService = new AdminStudentService(context, hasher, auditLogService);
        var jwtService = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new NPTELManagement.Infrastructure.Configuration.JwtOptions
            {
                Secret = "A_Very_Long_Secure_Secret_Key_At_Least_32_Chars_Long!",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryMinutes = 60
            }));

        var controller = new AuthController(
            userRepo,
            studentRepo,
            staffRepo,
            adminRepo,
            hasher,
            jwtService,
            rateLimiter,
            auditLogRepo,
            adminStudentService,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        // 1. Student self-registers
        var registerDto = new StudentSelfRegisterDto
        {
            Name = "Jeyaram Thiyagarajan",
            RegisterNumber = "951223104039",
            Department = "CSE",
            ClassSection = "A",
            Year = 4,
            Semester = 7,
            AcademicYear = "2026-27",
            Email = "jeyar7492@gmail.com",
            Phone = "9876543210",
            Password = "StudentPassword123!",
            ConfirmPassword = "StudentPassword123!"
        };

        var regResult = await controller.StudentRegister(registerDto, CancellationToken.None);
        var createdResult = Assert.IsType<ObjectResult>(regResult);
        Assert.Equal(201, createdResult.StatusCode);

        // 2. Student logs in with their Register Number
        var loginByReg = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "951223104039",
            Password = "StudentPassword123!"
        }, CancellationToken.None);

        var okByReg = Assert.IsType<OkObjectResult>(loginByReg);
        var respByReg = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okByReg.Value);
        Assert.True(respByReg.Success);
        Assert.Equal("951223104039", respByReg.Data!.Identifier);

        // 3. Student logs in with their Email (the exact production failure case!)
        var loginByEmail = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "jeyar7492@gmail.com",
            Password = "StudentPassword123!"
        }, CancellationToken.None);

        var okByEmail = Assert.IsType<OkObjectResult>(loginByEmail);
        var respByEmail = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okByEmail.Value);
        Assert.True(respByEmail.Success);
        Assert.Equal("951223104039", respByEmail.Data!.Identifier);

        // 4. Student logs in with their Email in uppercase
        var loginByUpperEmail = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "JEYAR7492@GMAIL.COM",
            Password = "StudentPassword123!"
        }, CancellationToken.None);

        var okByUpperEmail = Assert.IsType<OkObjectResult>(loginByUpperEmail);
        var respByUpperEmail = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okByUpperEmail.Value);
        Assert.True(respByUpperEmail.Success);

        // 5. Student logs in with their Name (as seen in audit logs)
        var loginByName = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "Jeyaram Thiyagarajan",
            Password = "StudentPassword123!"
        }, CancellationToken.None);

        var okByName = Assert.IsType<OkObjectResult>(loginByName);
        var respByName = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okByName.Value);
        Assert.True(respByName.Success);

        // 6. Invalid password is rejected
        var loginWrongPass = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "951223104039",
            Password = "WrongPassword999!"
        }, CancellationToken.None);

        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(loginWrongPass);
        var unauthResp = Assert.IsType<ApiResponse<object>>(unauthResult.Value);
        Assert.False(unauthResp.Success);
        Assert.Equal("Invalid credentials.", unauthResp.Message);

        // 7. Non-existent identifier is rejected
        var loginNonExistent = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "NON_EXISTENT_ID_999",
            Password = "StudentPassword123!"
        }, CancellationToken.None);

        var notFoundResult = Assert.IsType<UnauthorizedObjectResult>(loginNonExistent);
        var notFoundResp = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.False(notFoundResp.Success);
        Assert.Equal("Invalid credentials.", notFoundResp.Message);
    }

    [Fact]
    public async Task StudentLogin_MultipleAccountsWithSameEmail_LogsInToMatchingAccount()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();
        var studentRepo = new StudentRepository(context);
        var userRepo = new UserRepository(context);
        var staffRepo = new StaffRepository(context);
        var adminRepo = new AdminRepository(context);
        var rateLimiter = new LoginRateLimiter();
        var auditLogRepo = new AuditLogRepository(context);
        var auditLogService = new AuditLogService(context);
        var adminStudentService = new AdminStudentService(context, hasher, auditLogService);
        var jwtService = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new NPTELManagement.Infrastructure.Configuration.JwtOptions
            {
                Secret = "A_Very_Long_Secure_Secret_Key_At_Least_32_Chars_Long!",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryMinutes = 60
            }));

        var controller = new AuthController(
            userRepo,
            studentRepo,
            staffRepo,
            adminRepo,
            hasher,
            jwtService,
            rateLimiter,
            auditLogRepo,
            adminStudentService,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        // Register Account 1 with email shared@college.edu and Password A
        await controller.StudentRegister(new StudentSelfRegisterDto
        {
            Name = "Student One",
            RegisterNumber = "951000000001",
            Department = "CSE",
            ClassSection = "A",
            Year = 1,
            Semester = 1,
            AcademicYear = "2026-27",
            Email = "shared@college.edu",
            Phone = "9876543211",
            Password = "PasswordOne@123",
            ConfirmPassword = "PasswordOne@123"
        }, CancellationToken.None);

        // Register Account 2 with same email shared@college.edu and Password B
        await controller.StudentRegister(new StudentSelfRegisterDto
        {
            Name = "Student Two",
            RegisterNumber = "951000000002",
            Department = "CSE",
            ClassSection = "B",
            Year = 2,
            Semester = 3,
            AcademicYear = "2026-27",
            Email = "shared@college.edu",
            Phone = "9876543212",
            Password = "PasswordTwo@456",
            ConfirmPassword = "PasswordTwo@456"
        }, CancellationToken.None);

        // Logging in with email shared@college.edu + PasswordOne logs into Account 1
        var login1 = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "shared@college.edu",
            Password = "PasswordOne@123"
        }, CancellationToken.None);
        var ok1 = Assert.IsType<OkObjectResult>(login1);
        var resp1 = Assert.IsType<ApiResponse<AuthSuccessResponse>>(ok1.Value);
        Assert.Equal("951000000001", resp1.Data!.Identifier);

        // Logging in with email shared@college.edu + PasswordTwo logs into Account 2
        var login2 = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "shared@college.edu",
            Password = "PasswordTwo@456"
        }, CancellationToken.None);
        var ok2 = Assert.IsType<OkObjectResult>(login2);
        var resp2 = Assert.IsType<ApiResponse<AuthSuccessResponse>>(ok2.Value);
        Assert.Equal("951000000002", resp2.Data!.Identifier);
    }

    [Fact]
    public async Task StudentLogin_DiagnosticChecks_VerifyExactFailureBranches()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();
        var studentRepo = new StudentRepository(context);
        var userRepo = new UserRepository(context);
        var staffRepo = new StaffRepository(context);
        var adminRepo = new AdminRepository(context);
        var rateLimiter = new LoginRateLimiter();
        var auditLogRepo = new AuditLogRepository(context);
        var auditLogService = new AuditLogService(context);
        var adminStudentService = new AdminStudentService(context, hasher, auditLogService);
        var jwtService = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new NPTELManagement.Infrastructure.Configuration.JwtOptions
            {
                Secret = "A_Very_Long_Secure_Secret_Key_At_Least_32_Chars_Long!",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryMinutes = 60
            }));

        var controller = new AuthController(
            userRepo,
            studentRepo,
            staffRepo,
            adminRepo,
            hasher,
            jwtService,
            rateLimiter,
            auditLogRepo,
            adminStudentService,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        // 1. Setup synthetic student with inactive user account
        var inactiveUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "951000000099",
            Email = "inactive@college.edu",
            PasswordHash = hasher.HashPassword("ValidPassword123!"),
            Role = UserRole.Student,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var inactiveStudent = new Student
        {
            StudentId = Guid.NewGuid(),
            UserId = inactiveUser.Id,
            User = inactiveUser,
            Name = "Inactive Student",
            RegisterNumber = "951000000099",
            Department = "CSE",
            ClassSection = "A",
            Year = 3,
            Semester = 5,
            AcademicYear = "2026-27",
            Email = "inactive@college.edu",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(inactiveUser);
        context.Students.Add(inactiveStudent);

        // 2. Setup synthetic student with non-student role (e.g., Staff role assigned to User)
        var nonStudentUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "951000000088",
            Email = "nonstudent@college.edu",
            PasswordHash = hasher.HashPassword("ValidPassword123!"),
            Role = UserRole.Staff,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var nonStudentProfile = new Student
        {
            StudentId = Guid.NewGuid(),
            UserId = nonStudentUser.Id,
            User = nonStudentUser,
            Name = "Mismatched Role Student",
            RegisterNumber = "951000000088",
            Department = "CSE",
            ClassSection = "A",
            Year = 3,
            Semester = 5,
            AcademicYear = "2026-27",
            Email = "nonstudent@college.edu",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(nonStudentUser);
        context.Students.Add(nonStudentProfile);

        // 3. Setup standard active student
        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "951000000077",
            Email = "active@college.edu",
            PasswordHash = hasher.HashPassword("ValidPassword123!"),
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var activeStudent = new Student
        {
            StudentId = Guid.NewGuid(),
            UserId = activeUser.Id,
            User = activeUser,
            Name = "Active Student",
            RegisterNumber = "951000000077",
            Department = "CSE",
            ClassSection = "A",
            Year = 3,
            Semester = 5,
            AcademicYear = "2026-27",
            Email = "active@college.edu",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(activeUser);
        context.Students.Add(activeStudent);
        await context.SaveChangesAsync();

        // Check A: Inactive account returns 401 "Invalid credentials."
        var resInactive = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "951000000099",
            Password = "ValidPassword123!"
        }, CancellationToken.None);
        var unauthInactive = Assert.IsType<UnauthorizedObjectResult>(resInactive);
        var respInactive = Assert.IsType<ApiResponse<object>>(unauthInactive.Value);
        Assert.Equal("Invalid credentials.", respInactive.Message);

        // Check B: Role mismatch returns 401 "Invalid credentials."
        var resRoleMismatch = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "951000000088",
            Password = "ValidPassword123!"
        }, CancellationToken.None);
        var unauthRole = Assert.IsType<UnauthorizedObjectResult>(resRoleMismatch);
        var respRole = Assert.IsType<ApiResponse<object>>(unauthRole.Value);
        Assert.Equal("Invalid credentials.", respRole.Message);

        // Check C: Non-existent identifier returns 401 "Invalid credentials."
        var resNonExistent = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "999999999999",
            Password = "ValidPassword123!"
        }, CancellationToken.None);
        var unauthNonExistent = Assert.IsType<UnauthorizedObjectResult>(resNonExistent);
        var respNonExistent = Assert.IsType<ApiResponse<object>>(unauthNonExistent.Value);
        Assert.Equal("Invalid credentials.", respNonExistent.Message);

        // Check D: Wrong password for active account returns 401 "Invalid credentials."
        var resWrongPassword = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "951000000077",
            Password = "WrongPassword999!"
        }, CancellationToken.None);
        var unauthWrongPw = Assert.IsType<UnauthorizedObjectResult>(resWrongPassword);
        var respWrongPw = Assert.IsType<ApiResponse<object>>(unauthWrongPw.Value);
        Assert.Equal("Invalid credentials.", respWrongPw.Message);

        // Check E: Whitespace padded identifier matches and succeeds
        var resWhitespaceReg = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "  951000000077  ",
            Password = "ValidPassword123!"
        }, CancellationToken.None);
        var okWhitespaceReg = Assert.IsType<OkObjectResult>(resWhitespaceReg);
        var respWhitespaceReg = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okWhitespaceReg.Value);
        Assert.Equal("951000000077", respWhitespaceReg.Data!.Identifier);

        // Check F: Whitespace padded email matches and succeeds
        var resWhitespaceEmail = await controller.StudentLogin(new StudentLoginDto
        {
            RegisterNumber = "  active@college.edu  ",
            Password = "ValidPassword123!"
        }, CancellationToken.None);
        var okWhitespaceEmail = Assert.IsType<OkObjectResult>(resWhitespaceEmail);
        var respWhitespaceEmail = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okWhitespaceEmail.Value);
        Assert.Equal("951000000077", respWhitespaceEmail.Data!.Identifier);
    }
}
