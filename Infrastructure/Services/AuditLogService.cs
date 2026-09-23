using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;

    // Redaction patterns to guarantee passwords, hashes, and tokens are NEVER persisted to logs
    private static readonly Regex SecretSanitizer = new(
        @"(password|hash|token|secret|jwt)\s*[:=]\s*['""]?[^,;}\s'""]+['""]?", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AuditLogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string action, string details, Guid? userId = null, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        // Sanitize sensitive values
        var sanitizedDetails = SecretSanitizer.Replace(details, "$1: [REDACTED]");

        var log = new AuditLog
        {
            LogId = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = sanitizedDetails,
            IpAddress = ipAddress ?? "127.0.0.1",
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task LogActionAsync(Guid? userId, string action, string details, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        return LogAsync(action, details, userId, ipAddress, cancellationToken);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(search) || 
                                     (a.Details != null && a.Details.ToLower().Contains(search)) ||
                                     (a.User != null && a.User.Username.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            query = query.Where(a => a.User != null && a.User.Role.ToString() == filter.Role);
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.DateTo.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                LogId = a.LogId,
                UserId = a.UserId,
                Username = a.User != null ? a.User.Username : "System",
                Role = a.User != null ? a.User.Role.ToString() : "Admin",
                Action = a.Action,
                Details = a.Details,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
