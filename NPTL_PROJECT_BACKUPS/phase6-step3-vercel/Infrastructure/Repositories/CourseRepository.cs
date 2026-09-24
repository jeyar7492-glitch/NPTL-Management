using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly ApplicationDbContext _context;

    public CourseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Course?> GetByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken);
    }

    public async Task<Course?> GetByCodeAsync(string courseCode, CancellationToken cancellationToken = default)
    {
        return await _context.Courses.FirstOrDefaultAsync(c => c.CourseCode.ToUpper() == courseCode.ToUpper(), cancellationToken);
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Courses.AsNoTracking().OrderBy(c => c.CourseName).ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Courses.CountAsync(cancellationToken);
    }
}
