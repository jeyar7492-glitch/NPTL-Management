using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Repositories;

public class RegistrationRepository : IRegistrationRepository
{
    private readonly ApplicationDbContext _context;

    public RegistrationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NptelRegistration?> GetByIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        return await _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .Include(r => r.Timeline)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);
    }

    public async Task<IReadOnlyList<NptelRegistration>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.NptelRegistrations
            .AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.EnrollmentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.NptelRegistrations.CountAsync(cancellationToken);
    }
}
