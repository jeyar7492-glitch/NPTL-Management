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
}
