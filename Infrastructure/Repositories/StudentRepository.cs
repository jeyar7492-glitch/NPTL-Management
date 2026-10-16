using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly ApplicationDbContext _context;

    public StudentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Student?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);
    }

    public async Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Student>> GetByIdentifierCandidatesAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Array.Empty<Student>();
        }

        var normalized = identifier.Trim().ToUpper();

        // 1. Direct match on RegisterNumber (highest priority, unique index)
        var exact = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RegisterNumber.ToUpper() == normalized, cancellationToken);

        if (exact != null)
        {
            return new[] { exact };
        }

        // 2. Query candidates by Email (Student or User), Username, or Name
        return await _context.Students
            .Include(s => s.User)
            .Where(s =>
                (s.Email != null && s.Email.ToUpper() == normalized) ||
                (s.User != null && s.User.Email != null && s.User.Email.ToUpper() == normalized) ||
                (s.User != null && s.User.Username.ToUpper() == normalized) ||
                s.Name.ToUpper() == normalized)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Student?> GetByRegisterNumberAsync(string registerNumber, CancellationToken cancellationToken = default)
    {
        var candidates = await GetByIdentifierCandidatesAsync(registerNumber, cancellationToken);
        return candidates.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Student>> GetAssignedStudentsAsync(
        string department,
        int year,
        string? classSection,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Students
            .AsNoTracking()
            .Where(s => s.Department.ToUpper() == department.ToUpper() && s.Year == year);

        if (!string.IsNullOrWhiteSpace(classSection))
        {
            query = query.Where(s => s.ClassSection != null && s.ClassSection.ToUpper() == classSection.ToUpper());
        }

        return await query.OrderBy(s => s.RegisterNumber).ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Students.CountAsync(cancellationToken);
    }
}
