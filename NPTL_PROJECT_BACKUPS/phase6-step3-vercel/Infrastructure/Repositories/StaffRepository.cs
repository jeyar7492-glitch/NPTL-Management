using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly ApplicationDbContext _context;

    public StaffRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Staff?> GetByIdAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        return await _context.StaffMembers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);
    }

    public async Task<Staff?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.StaffMembers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<Staff?> GetByIdentifierAsync(string staffIdentifier, CancellationToken cancellationToken = default)
    {
        return await _context.StaffMembers
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffIdentifier.ToUpper() == staffIdentifier.ToUpper(), cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StaffMembers.CountAsync(cancellationToken);
    }
}
