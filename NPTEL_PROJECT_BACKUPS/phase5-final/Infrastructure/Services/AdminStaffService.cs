using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AdminStaffService : IAdminStaffService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public AdminStaffService(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminStaffListDto>> GetStaffMembersAsync(
        string? search, 
        string? department, 
        int? assignedYear, 
        string? assignedClass, 
        bool? isActive, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.StaffMembers
            .Include(s => s.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(st => st.StaffName.ToLower().Contains(s) || 
                                      st.StaffIdentifier.ToLower().Contains(s) ||
                                      (st.User != null && st.User.Email != null && st.User.Email.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(st => st.Department == department);
        }

        if (assignedYear.HasValue && assignedYear.Value > 0)
        {
            query = query.Where(st => st.AssignedYear == assignedYear.Value);
        }

        if (!string.IsNullOrWhiteSpace(assignedClass))
        {
            query = query.Where(st => st.AssignedClass == assignedClass);
        }

        if (isActive.HasValue)
        {
            query = query.Where(st => st.User != null && st.User.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var p = Math.Max(1, page);
        var ps = Math.Clamp(pageSize, 1, 100);

        var staffList = await query
            .OrderBy(st => st.Department)
            .ThenBy(st => st.AssignedYear)
            .ThenBy(st => st.AssignedClass)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync(cancellationToken);

        var items = new List<AdminStaffListDto>();
        foreach (var st in staffList)
        {
            var assignedStudentCount = await _context.Students
                .CountAsync(s => s.Department == st.Department &&
                                 s.Year == st.AssignedYear &&
                                 s.ClassSection == st.AssignedClass, cancellationToken);

            items.Add(new AdminStaffListDto
            {
                StaffId = st.StaffId,
                UserId = st.UserId,
                StaffName = st.StaffName,
                StaffIdentifier = st.StaffIdentifier,
                Department = st.Department,
                AssignedYear = st.AssignedYear,
                AssignedClass = st.AssignedClass,
                Email = st.User?.Email,
                IsActive = st.User?.IsActive ?? true,
                AssignedStudentCount = assignedStudentCount,
                CreatedAt = st.CreatedAt
            });
        }

        return new PagedResult<AdminStaffListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = p,
            PageSize = ps
        };
    }

    public async Task<AdminStaffDetailDto?> GetStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffMembers
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);

        if (staff == null)
            return null;

        var assignedCount = await _context.Students
            .CountAsync(s => s.Department == staff.Department &&
                             s.Year == staff.AssignedYear &&
                             s.ClassSection == staff.AssignedClass, cancellationToken);

        return new AdminStaffDetailDto
        {
            StaffId = staff.StaffId,
            UserId = staff.UserId,
            StaffName = staff.StaffName,
            StaffIdentifier = staff.StaffIdentifier,
            Department = staff.Department,
            AssignedYear = staff.AssignedYear,
            AssignedClass = staff.AssignedClass,
            Email = staff.User?.Email,
            IsActive = staff.User?.IsActive ?? true,
            AssignedStudentCount = assignedCount,
            CreatedAt = staff.CreatedAt
        };
    }

    public async Task<AdminStaffDetailDto> CreateStaffAsync(CreateStaffDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.StaffName))
            throw new ArgumentException("Staff name is required.");
        if (string.IsNullOrWhiteSpace(dto.StaffIdentifier))
            throw new ArgumentException("Staff identifier is required.");
        if (dto.AssignedYear < 1 || dto.AssignedYear > 4)
            throw new ArgumentException("Assigned year must be between 1 and 4.");

        var cleanId = dto.StaffIdentifier.Trim();
        if (await _context.StaffMembers.AnyAsync(s => s.StaffIdentifier == cleanId, cancellationToken) ||
            await _context.Users.AnyAsync(u => u.Username == cleanId, cancellationToken))
        {
            throw new InvalidOperationException($"Staff member with identifier '{cleanId}' already exists.");
        }

        var rawPassword = !string.IsNullOrWhiteSpace(dto.InitialPassword) ? dto.InitialPassword : "Staff@Nptel2026";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = cleanId,
            PasswordHash = _passwordHasher.HashPassword(rawPassword),
            Role = UserRole.Staff,
            Email = dto.Email?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var staff = new Staff
        {
            StaffId = Guid.NewGuid(),
            UserId = user.Id,
            StaffName = dto.StaffName.Trim(),
            StaffIdentifier = cleanId,
            Department = !string.IsNullOrWhiteSpace(dto.Department) ? dto.Department.Trim() : "CSE",
            AssignedYear = dto.AssignedYear,
            AssignedClass = dto.AssignedClass?.Trim() ?? "A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.StaffMembers.Add(staff);

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "Staff Created", 
            $"Created faculty {staff.StaffName} ({staff.StaffIdentifier}) assigned to {staff.Department} Y{staff.AssignedYear} Sec {staff.AssignedClass}", 
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return (await GetStaffByIdAsync(staff.StaffId, cancellationToken))!;
    }

    public async Task<AdminStaffDetailDto> UpdateStaffAsync(Guid staffId, UpdateStaffDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffMembers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);

        if (staff == null)
            throw new KeyNotFoundException("Staff record not found.");

        if (string.IsNullOrWhiteSpace(dto.StaffName))
            throw new ArgumentException("Staff name is required.");
        if (dto.AssignedYear < 1 || dto.AssignedYear > 4)
            throw new ArgumentException("Assigned year must be between 1 and 4.");

        staff.StaffName = dto.StaffName.Trim();
        staff.Department = !string.IsNullOrWhiteSpace(dto.Department) ? dto.Department.Trim() : staff.Department;
        staff.AssignedYear = dto.AssignedYear;
        staff.AssignedClass = dto.AssignedClass?.Trim() ?? staff.AssignedClass;
        staff.UpdatedAt = DateTime.UtcNow;

        if (staff.User != null && !string.IsNullOrWhiteSpace(dto.Email))
        {
            staff.User.Email = dto.Email.Trim();
            staff.User.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "Staff Updated", 
            $"Updated faculty {staff.StaffName} ({staff.StaffIdentifier}) assignment to {staff.Department} Y{staff.AssignedYear} Sec {staff.AssignedClass}", 
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return (await GetStaffByIdAsync(staffId, cancellationToken))!;
    }

    public async Task<bool> UpdateStaffStatusAsync(Guid staffId, bool isActive, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffMembers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);

        if (staff == null || staff.User == null)
            throw new KeyNotFoundException("Staff record not found.");

        staff.User.IsActive = isActive;
        staff.User.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var action = isActive ? "Staff Activated" : "Staff Deactivated";
        await _auditLogService.LogAsync(
            action, 
            $"Set active status of faculty {staff.StaffName} ({staff.StaffIdentifier}) to {isActive}", 
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return true;
    }

    public async Task<bool> ResetStaffPasswordAsync(Guid staffId, ResetStaffPasswordDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");

        var staff = await _context.StaffMembers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);

        if (staff == null || staff.User == null)
            throw new KeyNotFoundException("Staff record not found.");

        staff.User.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
        staff.User.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "Password Reset", 
            $"Reset password for faculty account {staff.StaffIdentifier}", 
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return true;
    }
}
