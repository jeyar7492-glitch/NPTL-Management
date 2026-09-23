using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Authentication;

public class StaffAuthorizationService : IStaffAuthorizationService
{
    private readonly ApplicationDbContext _context;

    public StaffAuthorizationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanStaffAccessStudentAsync(Guid staffUserId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == staffUserId || s.StaffId == staffUserId, cancellationToken);

        if (staff == null)
            return false;

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        if (student == null)
            return false;

        // Rule 1: Same Department
        if (!string.Equals(staff.Department, student.Department, StringComparison.OrdinalIgnoreCase))
            return false;

        // Rule 2: Assigned Year matches Student Year
        if (staff.AssignedYear != student.Year)
            return false;

        // Rule 3: If staff is assigned to a specific class section, student must match
        if (!string.IsNullOrWhiteSpace(staff.AssignedClass))
        {
            if (!string.Equals(staff.AssignedClass, student.ClassSection, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public async Task<bool> CanStaffAccessScopeAsync(Guid staffUserId, string department, int year, string? classSection, CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == staffUserId || s.StaffId == staffUserId, cancellationToken);

        if (staff == null)
            return false;

        if (!string.Equals(staff.Department, department, StringComparison.OrdinalIgnoreCase))
            return false;

        if (staff.AssignedYear != year)
            return false;

        if (!string.IsNullOrWhiteSpace(staff.AssignedClass) && !string.IsNullOrWhiteSpace(classSection))
        {
            if (!string.Equals(staff.AssignedClass, classSection, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
