using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly ApplicationDbContext _context;

    public AdminRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Admin?> GetByIdAsync(Guid adminId, CancellationToken cancellationToken = default)
    {
        return await _context.Admins
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.AdminId == adminId, cancellationToken);
    }

    public async Task<Admin?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Admins
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task<Admin?> GetByIdentifierAsync(string adminIdentifier, CancellationToken cancellationToken = default)
    {
        return await _context.Admins
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.AdminIdentifier.ToUpper() == adminIdentifier.ToUpper(), cancellationToken);
    }
}
