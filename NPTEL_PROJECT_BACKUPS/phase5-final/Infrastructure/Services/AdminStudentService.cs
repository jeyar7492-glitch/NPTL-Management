using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AdminStudentService : IAdminStudentService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public AdminStudentService(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminStudentListDto>> GetStudentsAsync(
        string? search, 
        string? department, 
        int? year, 
        string? classSection, 
        bool? isActive, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Students
            .Include(s => s.User)
            .Include(s => s.Registrations)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(st => st.Name.ToLower().Contains(s) || 
                                      st.RegisterNumber.ToLower().Contains(s) ||
                                      (st.Email != null && st.Email.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(st => st.Department == department);
        }

        if (year.HasValue && year.Value > 0)
        {
            query = query.Where(st => st.Year == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(classSection))
        {
            query = query.Where(st => st.ClassSection == classSection);
        }

        if (isActive.HasValue)
        {
            query = query.Where(st => st.User != null && st.User.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var p = Math.Max(1, page);
        var ps = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderBy(st => st.Year)
            .ThenBy(st => st.ClassSection)
            .ThenBy(st => st.RegisterNumber)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(st => new AdminStudentListDto
            {
                StudentId = st.StudentId,
                UserId = st.UserId,
                Name = st.Name,
                RegisterNumber = st.RegisterNumber,
                Department = st.Department,
                ClassSection = st.ClassSection,
                Year = st.Year,
                Batch = st.Batch,
                Email = st.Email,
                Phone = st.Phone,
                IsActive = st.User != null && st.User.IsActive,
                RegisteredCoursesCount = st.Registrations.Count,
                CreatedAt = st.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminStudentListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = p,
            PageSize = ps
        };
    }

    public async Task<AdminStudentDetailDto?> GetStudentByIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Course)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.ExamStatus)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Certificate)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        if (student == null)
        {
            return null;
        }

        return new AdminStudentDetailDto
        {
            StudentId = student.StudentId,
            UserId = student.UserId,
            Name = student.Name,
            RegisterNumber = student.RegisterNumber,
            Department = student.Department,
            ClassSection = student.ClassSection,
            Year = student.Year,
            Batch = student.Batch,
            Email = student.Email,
            Phone = student.Phone,
            IsActive = student.User?.IsActive ?? true,
            CreatedAt = student.CreatedAt,
            Registrations = student.Registrations.Select(r => new AdminRegistrationDto
            {
                RegistrationId = r.RegistrationId,
                StudentId = student.StudentId,
                StudentName = student.Name,
                RegisterNumber = student.RegisterNumber,
                Department = student.Department,
                Year = student.Year,
                ClassSection = student.ClassSection,
                CourseId = r.CourseId,
                CourseCode = r.Course?.CourseCode ?? string.Empty,
                CourseName = r.Course?.CourseName ?? string.Empty,
                DurationWeeks = r.Course?.DurationWeeks ?? 0,
                EnrollmentDate = r.EnrollmentDate,
                Status = r.Status.ToString(),
                ExamApplicationStatus = r.ExamStatus != null ? (r.ExamStatus.ExamApplicationStatus ?? "NotStarted") : "NotStarted",
                CertificateStatus = r.Certificate != null ? r.Certificate.VerifiedStatus.ToString() : "Pending"
            }).ToList()
        };
    }

    public async Task<AdminStudentDetailDto> CreateStudentAsync(CreateStudentDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Student name is required.");
        if (string.IsNullOrWhiteSpace(dto.RegisterNumber))
            throw new ArgumentException("Register number is required.");
        if (dto.Year < 1 || dto.Year > 4)
            throw new ArgumentException("Year must be between 1 and 4.");

        var cleanReg = dto.RegisterNumber.Trim();
        if (await _context.Students.AnyAsync(s => s.RegisterNumber == cleanReg, cancellationToken) ||
            await _context.Users.AnyAsync(u => u.Username == cleanReg, cancellationToken))
        {
            throw new InvalidOperationException($"A student or user with register number '{cleanReg}' already exists.");
        }

        // 2. Create User identity
        var rawPassword = !string.IsNullOrWhiteSpace(dto.InitialPassword) ? dto.InitialPassword : "Student@Nptel2026";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = cleanReg,
            PasswordHash = _passwordHasher.HashPassword(rawPassword),
            Role = UserRole.Student,
            Email = dto.Email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // 3. Create Student profile
        var student = new Student
        {
            StudentId = Guid.NewGuid(),
            UserId = user.Id,
            Name = dto.Name.Trim(),
            RegisterNumber = cleanReg,
            Department = !string.IsNullOrWhiteSpace(dto.Department) ? dto.Department.Trim() : "CSE",
            ClassSection = dto.ClassSection?.Trim() ?? "A",
            Year = dto.Year,
            Batch = dto.Batch?.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Students.Add(student);

        await _context.SaveChangesAsync(cancellationToken);

        // 4. Audit Log
        await _auditLogService.LogAsync(
            "Student Created", 
            $"Created student {student.Name} ({student.RegisterNumber}) in {student.Department} Y{student.Year} Sec {student.ClassSection}",
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return new AdminStudentDetailDto
        {
            StudentId = student.StudentId,
            UserId = user.Id,
            Name = student.Name,
            RegisterNumber = student.RegisterNumber,
            Department = student.Department,
            ClassSection = student.ClassSection,
            Year = student.Year,
            Batch = student.Batch,
            Email = student.Email,
            Phone = student.Phone,
            IsActive = user.IsActive,
            CreatedAt = student.CreatedAt,
            Registrations = new()
        };
    }

    public async Task<AdminStudentDetailDto> UpdateStudentAsync(Guid studentId, UpdateStudentDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        if (student == null)
            throw new KeyNotFoundException("Student record not found.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Student name is required.");
        if (dto.Year < 1 || dto.Year > 4)
            throw new ArgumentException("Year must be between 1 and 4.");

        student.Name = dto.Name.Trim();
        student.Department = !string.IsNullOrWhiteSpace(dto.Department) ? dto.Department.Trim() : student.Department;
        student.ClassSection = dto.ClassSection?.Trim() ?? student.ClassSection;
        student.Year = dto.Year;
        student.Batch = dto.Batch?.Trim();
        student.Email = dto.Email?.Trim();
        student.Phone = dto.Phone?.Trim();
        student.UpdatedAt = DateTime.UtcNow;

        if (student.User != null && !string.IsNullOrWhiteSpace(dto.Email))
        {
            student.User.Email = dto.Email.Trim();
            student.User.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "Student Updated", 
            $"Updated details for student {student.Name} ({student.RegisterNumber})", 
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return (await GetStudentByIdAsync(studentId, cancellationToken))!;
    }

    public async Task<bool> UpdateStudentStatusAsync(Guid studentId, bool isActive, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        if (student == null || student.User == null)
            throw new KeyNotFoundException("Student record not found.");

        student.User.IsActive = isActive;
        student.User.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var action = isActive ? "Student Activated" : "Student Deactivated";
        await _auditLogService.LogAsync(
            action, 
            $"Set active status of student {student.Name} ({student.RegisterNumber}) to {isActive}", 
            adminUserId, 
            ipAddress, 
            cancellationToken);

        return true;
    }
}
