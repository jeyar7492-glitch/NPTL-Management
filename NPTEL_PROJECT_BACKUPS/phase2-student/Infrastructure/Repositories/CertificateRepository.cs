using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Repositories;

public class CertificateRepository : ICertificateRepository
{
    private readonly ApplicationDbContext _context;

    public CertificateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Certificate?> GetByIdAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        return await _context.Certificates
            .Include(c => c.Registration)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId, cancellationToken);
    }

    public async Task<Certificate?> GetByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        return await _context.Certificates
            .Include(c => c.Registration)
            .FirstOrDefaultAsync(c => c.RegistrationId == registrationId, cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Certificates.CountAsync(cancellationToken);
    }
}
