using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NPTELManagement.Api.Controllers;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Authentication;
using NPTELManagement.Infrastructure.Configuration;
using NPTELManagement.Infrastructure.Data;
using NPTELManagement.Infrastructure.Repositories;
using NPTELManagement.Infrastructure.Services;
using Xunit;

namespace NPTELManagement.Tests;

public class StaffManagementTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private (AdminStaffService staffService, IPasswordHasher hasher, ApplicationDbContext context) CreateStaffService()
    {
        var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();
        var auditService = new AuditLogService(context);
        var staffService = new AdminStaffService(context, hasher, auditService);
        return (staffService, hasher, context);
    }

    [Fact]
    public async Task CreateStaff_Succeeds_WithCustomCredentials_AndHashesPassword()
    {
        var (staffService, hasher, context) = CreateStaffService();

        var dto = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-101",
            StaffName = "Dr. Alan Turing",
            Username = "alan_turing",
            Email = "alan@college.edu",
            Department = "CSE",
            Designation = "Associate Professor",
            AssignedYear = 3,
            AssignedClass = "A",
            InitialPassword = "SecurePassword2026!",
            ConfirmPassword = "SecurePassword2026!"
        };

        var result = await staffService.CreateStaffAsync(dto, Guid.NewGuid(), "127.0.0.1");

        // Verify returned DTO
        Assert.NotNull(result);
        Assert.Equal("STF-CSE-101", result.StaffIdentifier);
        Assert.Equal("Dr. Alan Turing", result.StaffName);
        Assert.Equal("alan_turing", result.Username);
        Assert.Equal("alan@college.edu", result.Email);
        Assert.Equal("CSE", result.Department);
        Assert.True(result.IsActive);

        // Verify User in database
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "alan_turing");
        Assert.NotNull(user);
        Assert.Equal(UserRole.Staff, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("alan@college.edu", user.Email);

        // Verify password is NOT stored in plaintext, but matches via BCrypt
        Assert.NotEqual("SecurePassword2026!", user.PasswordHash);
        Assert.True(hasher.VerifyPassword("SecurePassword2026!", user.PasswordHash));

        // Verify Staff in database
        var staff = await context.StaffMembers.FirstOrDefaultAsync(s => s.StaffIdentifier == "STF-CSE-101");
        Assert.NotNull(staff);
        Assert.Equal(user.Id, staff.UserId);
        Assert.Equal(3, staff.AssignedYear);
        Assert.Equal("A", staff.AssignedClass);
    }

    [Theory]
    [InlineData("", "STF-01", "pass12345!", "pass12345!", "Staff Name is required.")]
    [InlineData("Prof. John", "", "pass12345!", "pass12345!", "Staff ID is required.")]
    [InlineData("Prof. John", "STF-01", "", "", "Password is required.")]
    [InlineData("Prof. John", "STF-01", "short", "short", "Password must be at least 8 characters long.")]
    [InlineData("Prof. John", "STF-01", "password123!", "different123!", "Password and Confirm Password do not match.")]
    public async Task CreateStaff_Validation_ThrowsArgumentException(
        string name, string id, string password, string confirmPassword, string expectedError)
    {
        var (staffService, _, _) = CreateStaffService();

        var dto = new CreateStaffDto
        {
            StaffName = name,
            StaffIdentifier = id,
            InitialPassword = password,
            ConfirmPassword = confirmPassword,
            AssignedYear = 1,
            AssignedClass = "A"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            staffService.CreateStaffAsync(dto, Guid.NewGuid(), "127.0.0.1"));

        Assert.Equal(expectedError, ex.Message);
    }

    [Fact]
    public async Task CreateStaff_InvalidEmailFormat_ThrowsArgumentException()
    {
        var (staffService, _, _) = CreateStaffService();

        var dto = new CreateStaffDto
        {
            StaffName = "Prof. John",
            StaffIdentifier = "STF-01",
            Email = "invalid-email-format",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            staffService.CreateStaffAsync(dto, Guid.NewGuid(), "127.0.0.1"));

        Assert.Equal("Invalid email address format.", ex.Message);
    }

    [Fact]
    public async Task CreateStaff_DuplicateStaffId_ThrowsInvalidOperationException()
    {
        var (staffService, _, _) = CreateStaffService();

        var dto1 = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-101",
            StaffName = "Prof. Alan",
            Username = "alan_1",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };
        await staffService.CreateStaffAsync(dto1, null, null);

        var dto2 = new CreateStaffDto
        {
            StaffIdentifier = "stf-cse-101", // case-insensitive duplicate check
            StaffName = "Prof. Bob",
            Username = "bob_2",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staffService.CreateStaffAsync(dto2, null, null));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateStaff_DuplicateUsername_ThrowsInvalidOperationException()
    {
        var (staffService, _, _) = CreateStaffService();

        var dto1 = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-101",
            StaffName = "Prof. Alan",
            Username = "common_user",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };
        await staffService.CreateStaffAsync(dto1, null, null);

        var dto2 = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-102",
            StaffName = "Prof. Bob",
            Username = "common_user",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staffService.CreateStaffAsync(dto2, null, null));

        Assert.Contains("username 'common_user' already exists", ex.Message);
    }

    [Fact]
    public async Task CreateStaff_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var (staffService, _, _) = CreateStaffService();

        var dto1 = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-101",
            StaffName = "Prof. Alan",
            Username = "alan_1",
            Email = "shared@college.edu",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };
        await staffService.CreateStaffAsync(dto1, null, null);

        var dto2 = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-102",
            StaffName = "Prof. Bob",
            Username = "bob_2",
            Email = "shared@college.edu",
            InitialPassword = "Password123!",
            ConfirmPassword = "Password123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staffService.CreateStaffAsync(dto2, null, null));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public async Task StaffLogin_WithStaffId_OrUsername_OrEmail_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();
        var auditService = new AuditLogService(context);
        var staffService = new AdminStaffService(context, hasher, auditService);
        var staffRepo = new StaffRepository(context);
        var userRepo = new UserRepository(context);
        var studentRepo = new StudentRepository(context);
        var adminRepo = new AdminRepository(context);
        var auditRepo = new AuditLogRepository(context);
        var rateLimiter = new LoginRateLimiter();
        var adminStudentService = new AdminStudentService(context, hasher, auditService);

        var jwtService = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Secret = "A_Very_Long_Secure_Secret_Key_At_Least_32_Chars_Long!",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryMinutes = 60
            }));

        var authController = new AuthController(
            userRepo,
            studentRepo,
            staffRepo,
            adminRepo,
            hasher,
            jwtService,
            rateLimiter,
            auditRepo,
            adminStudentService,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // 1. Admin creates staff account
        var createDto = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-301",
            StaffName = "Marie Curie",
            Username = "marie_curie",
            Email = "curie@college.edu",
            Department = "CSE",
            AssignedYear = 2,
            AssignedClass = "B",
            InitialPassword = "CuriePassword123!",
            ConfirmPassword = "CuriePassword123!"
        };
        await staffService.CreateStaffAsync(createDto, Guid.NewGuid(), "127.0.0.1");

        // 2. Login using Staff ID
        var loginById = await authController.StaffLogin(new StaffLoginDto
        {
            StaffId = "STF-CSE-301",
            Password = "CuriePassword123!"
        }, default);
        var okResult1 = Assert.IsType<OkObjectResult>(loginById);
        var resp1 = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okResult1.Value);
        Assert.True(resp1.Success);
        Assert.Equal("Staff", resp1.Data?.Role);
        Assert.Equal("STF-CSE-301", resp1.Data?.Identifier);

        // 3. Login using Username
        var loginByUsername = await authController.StaffLogin(new StaffLoginDto
        {
            StaffId = "marie_curie",
            Password = "CuriePassword123!"
        }, default);
        var okResult2 = Assert.IsType<OkObjectResult>(loginByUsername);
        var resp2 = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okResult2.Value);
        Assert.True(resp2.Success);
        Assert.Equal("Staff", resp2.Data?.Role);

        // 4. Login using Email
        var loginByEmail = await authController.StaffLogin(new StaffLoginDto
        {
            StaffId = "curie@college.edu",
            Password = "CuriePassword123!"
        }, default);
        var okResult3 = Assert.IsType<OkObjectResult>(loginByEmail);
        var resp3 = Assert.IsType<ApiResponse<AuthSuccessResponse>>(okResult3.Value);
        Assert.True(resp3.Success);

        // 5. Login with invalid password fails
        var loginBadPass = await authController.StaffLogin(new StaffLoginDto
        {
            StaffId = "STF-CSE-301",
            Password = "WrongPassword!"
        }, default);
        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(loginBadPass);
        var respBad = Assert.IsType<ApiResponse<object>>(unauthResult.Value);
        Assert.False(respBad.Success);
        Assert.Equal("Invalid credentials.", respBad.Message);
    }

    [Fact]
    public void AdminController_HasAuthorizeRoleAdmin_Attribute()
    {
        var type = typeof(AdminController);
        var authAttributes = type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(authAttributes);

        var authorize = Assert.IsType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(authAttributes[0]);
        Assert.Equal("Admin", authorize.Roles);
    }

    [Fact]
    public async Task AdminStaffService_StatusToggle_And_PasswordReset_Work()
    {
        var (staffService, hasher, context) = CreateStaffService();

        var dto = new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-401",
            StaffName = "Prof. Ada Lovelace",
            Username = "ada_lovelace",
            InitialPassword = "AdaPassword123!",
            ConfirmPassword = "AdaPassword123!",
            AssignedYear = 1,
            AssignedClass = "A"
        };
        var created = await staffService.CreateStaffAsync(dto, null, null);

        // Deactivate staff
        var deactivated = await staffService.UpdateStaffStatusAsync(created.StaffId, false, null, null);
        Assert.True(deactivated);

        var user = await context.Users.FirstAsync(u => u.Id == created.UserId);
        Assert.False(user.IsActive);

        // Reactivate staff
        var reactivated = await staffService.UpdateStaffStatusAsync(created.StaffId, true, null, null);
        Assert.True(reactivated);
        Assert.True(user.IsActive);

        // Reset password
        var reset = await staffService.ResetStaffPasswordAsync(created.StaffId, new ResetStaffPasswordDto
        {
            NewPassword = "NewSecretPassword2026!"
        }, null, null);
        Assert.True(reset);

        Assert.True(hasher.VerifyPassword("NewSecretPassword2026!", user.PasswordHash));
        Assert.False(hasher.VerifyPassword("AdaPassword123!", user.PasswordHash));
    }

    [Fact]
    public async Task AdminStaffService_GetStaffMembers_IncludesUsernameAndDesignation_AndSupportsSearch()
    {
        var (staffService, _, context) = CreateStaffService();

        await staffService.CreateStaffAsync(new CreateStaffDto
        {
            StaffIdentifier = "STF-CSE-501",
            StaffName = "Dr. Grace Hopper",
            Username = "grace_hopper",
            Email = "hopper@college.edu",
            Department = "CSE",
            Designation = "Professor & Head",
            AssignedYear = 4,
            AssignedClass = "A",
            InitialPassword = "HopperPassword123!",
            ConfirmPassword = "HopperPassword123!"
        }, null, null);

        await staffService.CreateStaffAsync(new CreateStaffDto
        {
            StaffIdentifier = "STF-ECE-502",
            StaffName = "Dr. Claude Shannon",
            Username = "claude_shannon",
            Email = "shannon@college.edu",
            Department = "ECE",
            Designation = "Assistant Professor",
            AssignedYear = 3,
            AssignedClass = "B",
            InitialPassword = "ShannonPassword123!",
            ConfirmPassword = "ShannonPassword123!"
        }, null, null);

        // Get all
        var all = await staffService.GetStaffMembersAsync(null, null, null, null, null, 1, 50);
        Assert.Equal(2, all.TotalCount);

        var hopper = all.Items.FirstOrDefault(s => s.StaffIdentifier == "STF-CSE-501");
        Assert.NotNull(hopper);
        Assert.Equal("grace_hopper", hopper.Username);
        Assert.Equal("hopper@college.edu", hopper.Email);
        Assert.Equal("CSE", hopper.Department);
        Assert.Contains("Faculty Advisor", hopper.Designation);

        // Search by username
        var searchUsername = await staffService.GetStaffMembersAsync("claude_shannon", null, null, null, null, 1, 50);
        Assert.Single(searchUsername.Items);
        Assert.Equal("STF-ECE-502", searchUsername.Items[0].StaffIdentifier);

        // Search by staff ID
        var searchId = await staffService.GetStaffMembersAsync("STF-CSE-501", null, null, null, null, 1, 50);
        Assert.Single(searchId.Items);
        Assert.Equal("Dr. Grace Hopper", searchId.Items[0].StaffName);

        // Search by department filter
        var searchDept = await staffService.GetStaffMembersAsync(null, "ECE", null, null, null, 1, 50);
        Assert.Single(searchDept.Items);
        Assert.Equal("STF-ECE-502", searchDept.Items[0].StaffIdentifier);
    }

    [Fact]
    public void StaffController_HasAuthorizeRoleStaff_Attribute()
    {
        var type = typeof(StaffController);
        var authAttributes = type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(authAttributes);

        var authorize = Assert.IsType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(authAttributes[0]);
        Assert.Equal("Staff", authorize.Roles);

        // StaffController must not contain any method to create staff
        var createStaffMethod = type.GetMethod("CreateStaff");
        Assert.Null(createStaffMethod);
    }

    [Fact]
    public void AdminController_CreateStaff_RequiresAdminRole_AndDisallowsAnonymous()
    {
        var type = typeof(AdminController);
        var authAttributes = type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(authAttributes);

        var authorize = Assert.IsType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(authAttributes[0]);
        Assert.Equal("Admin", authorize.Roles);

        var createStaffMethod = type.GetMethod("CreateStaff");
        Assert.NotNull(createStaffMethod);

        // Ensure no AllowAnonymous attribute overrides the controller authorization
        var anonAttributes = createStaffMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        Assert.Empty(anonAttributes);
    }

    [Fact]
    public void StaffDTOs_NeverExposePasswordOrPasswordHash()
    {
        var listDtoType = typeof(AdminStaffListDto);
        Assert.Null(listDtoType.GetProperty("Password"));
        Assert.Null(listDtoType.GetProperty("PasswordHash"));
        Assert.Null(listDtoType.GetProperty("InitialPassword"));

        var detailDtoType = typeof(AdminStaffDetailDto);
        Assert.Null(detailDtoType.GetProperty("Password"));
        Assert.Null(detailDtoType.GetProperty("PasswordHash"));
        Assert.Null(detailDtoType.GetProperty("InitialPassword"));
    }

    [Fact]
    public async Task CreateStaffAsync_ExecutionStrategy_HandlesRetryWithoutDuplicateError()
    {
        var (staffService, hasher, context) = CreateStaffService();

        var dto = new CreateStaffDto
        {
            StaffIdentifier = "STF-RETRY-001",
            StaffName = "Prof. Retry Test",
            Username = "retry_test",
            Email = "retry@college.edu",
            Department = "CSE",
            Designation = "Assistant Professor",
            AssignedYear = 2,
            AssignedClass = "B",
            InitialPassword = "RetryPassword2026!",
            ConfirmPassword = "RetryPassword2026!"
        };

        // First execution creates the staff member
        var firstResult = await staffService.CreateStaffAsync(dto, null, null);
        Assert.NotNull(firstResult);
        Assert.Equal("STF-RETRY-001", firstResult.StaffIdentifier);

        // When the execution strategy retries after an apparent transient network failure
        // where the records were already committed in the prior attempt with the exact matching username:
        var retryResult = await staffService.CreateStaffAsync(dto, null, null);
        Assert.NotNull(retryResult);
        Assert.Equal(firstResult.StaffId, retryResult.StaffId);
        Assert.Equal("STF-RETRY-001", retryResult.StaffIdentifier);

        // Verify only 1 User and 1 Staff record exist in the database (no duplicate or orphaned records)
        var userCount = await context.Users.CountAsync(u => u.Username == "retry_test");
        var staffCount = await context.StaffMembers.CountAsync(s => s.StaffIdentifier == "STF-RETRY-001");
        Assert.Equal(1, userCount);
        Assert.Equal(1, staffCount);
    }

    [Fact]
    public async Task CreateStaffAsync_CollisionWithDifferentUser_ThrowsInvalidOperationException()
    {
        var (staffService, hasher, context) = CreateStaffService();

        var dto1 = new CreateStaffDto
        {
            StaffIdentifier = "STF-COLLISION-001",
            StaffName = "Prof. Collision One",
            Username = "collision_user_one",
            Department = "CSE",
            InitialPassword = "CollisionPass1!",
            ConfirmPassword = "CollisionPass1!",
            AssignedYear = 1,
            AssignedClass = "A"
        };
        await staffService.CreateStaffAsync(dto1, null, null);

        // Attempt to create another staff member with same StaffIdentifier but different username
        var dto2 = new CreateStaffDto
        {
            StaffIdentifier = "STF-COLLISION-001",
            StaffName = "Prof. Collision Two",
            Username = "collision_user_two",
            Department = "CSE",
            InitialPassword = "CollisionPass2!",
            ConfirmPassword = "CollisionPass2!",
            AssignedYear = 1,
            AssignedClass = "A"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => staffService.CreateStaffAsync(dto2, null, null));
        Assert.Contains("already exists", ex.Message);
    }
}
