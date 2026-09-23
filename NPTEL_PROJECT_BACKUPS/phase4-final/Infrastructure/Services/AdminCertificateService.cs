using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class AdminCertificateService : IAdminCertificateService
{
    private const string BUCKET_NAME = "certificates";
    private const long MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB

    private readonly ApplicationDbContext _context;
    private readonly IPrivateCloudStorageService _storageService;
    private readonly IStaffAuthorizationService _staffAuthService;
    private readonly IAuditLogService _auditLogService;

    public AdminCertificateService(
        ApplicationDbContext context,
        IPrivateCloudStorageService storageService,
        IStaffAuthorizationService staffAuthService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _storageService = storageService;
        _staffAuthService = staffAuthService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AdminCertificateDto>> GetCertificatesAsync(
        string? search, 
        string? verifiedStatus, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Certificates
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Student)
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Course)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(verifiedStatus))
        {
            if (Enum.TryParse<CertificateStatus>(verifiedStatus.Trim(), true, out var parsedStatus))
            {
                query = query.Where(c => c.VerifiedStatus == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                (c.Registration != null && c.Registration.Student != null &&
                    (c.Registration.Student.Name.ToLower().Contains(s) || c.Registration.Student.RegisterNumber.ToLower().Contains(s))) ||
                (c.Registration != null && c.Registration.Course != null &&
                    (c.Registration.Course.CourseName.ToLower().Contains(s) || c.Registration.Course.CourseCode.ToLower().Contains(s))));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCertificateDto
            {
                CertificateId = c.CertificateId,
                RegistrationId = c.RegistrationId,
                StudentId = c.Registration != null ? c.Registration.StudentId : Guid.Empty,
                StudentName = c.Registration != null && c.Registration.Student != null ? c.Registration.Student.Name : string.Empty,
                RegisterNumber = c.Registration != null && c.Registration.Student != null ? c.Registration.Student.RegisterNumber : string.Empty,
                Department = c.Registration != null && c.Registration.Student != null ? c.Registration.Student.Department : string.Empty,
                Year = c.Registration != null && c.Registration.Student != null ? c.Registration.Student.Year : 0,
                ClassSection = c.Registration != null && c.Registration.Student != null ? c.Registration.Student.ClassSection : null,
                CourseName = c.Registration != null && c.Registration.Course != null ? c.Registration.Course.CourseName : string.Empty,
                CourseCode = c.Registration != null && c.Registration.Course != null ? c.Registration.Course.CourseCode : string.Empty,
                StoragePath = c.StoragePath,
                VerifiedStatus = c.VerifiedStatus.ToString(),
                SubmittedDate = c.SubmittedDate,
                VerifiedDate = c.VerifiedDate,
                ReceivedDate = c.ReceivedDate,
                IssuedDate = c.IssuedDate
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCertificateDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminCertificateDto?> GetCertificateByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var c = await _context.Certificates
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Student)
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Course)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.RegistrationId == registrationId, cancellationToken);

        if (c == null) return null;

        return new AdminCertificateDto
        {
            CertificateId = c.CertificateId,
            RegistrationId = c.RegistrationId,
            StudentId = c.Registration?.StudentId ?? Guid.Empty,
            StudentName = c.Registration?.Student?.Name ?? string.Empty,
            RegisterNumber = c.Registration?.Student?.RegisterNumber ?? string.Empty,
            Department = c.Registration?.Student?.Department ?? string.Empty,
            Year = c.Registration?.Student?.Year ?? 0,
            ClassSection = c.Registration?.Student?.ClassSection,
            CourseName = c.Registration?.Course?.CourseName ?? string.Empty,
            CourseCode = c.Registration?.Course?.CourseCode ?? string.Empty,
            StoragePath = c.StoragePath,
            VerifiedStatus = c.VerifiedStatus.ToString(),
            SubmittedDate = c.SubmittedDate,
            VerifiedDate = c.VerifiedDate,
            ReceivedDate = c.ReceivedDate,
            IssuedDate = c.IssuedDate
        };
    }

    public async Task<AdminCertificateDto> UploadCertificateAsync(
        Guid registrationId, 
        Stream fileStream, 
        string fileName, 
        string contentType, 
        long fileLength, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        // 1. Validation
        if (fileStream == null || fileLength <= 0)
            throw new ArgumentException("Certificate file stream cannot be empty.");

        if (fileLength > MAX_FILE_SIZE)
            throw new ArgumentException($"File size exceeds the 10 MB limit. Actual: {fileLength / 1024 / 1024} MB.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".pdf")
            throw new ArgumentException("Only .pdf certificate files are allowed.");

        if (!string.IsNullOrWhiteSpace(contentType) && 
            !contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid content type. Must be application/pdf.");
        }

        // Magic bytes check: %PDF (0x25 0x50 0x44 0x46)
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
            var header = new byte[4];
            var bytesRead = await fileStream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
            fileStream.Position = 0;

            if (bytesRead < 4 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
            {
                throw new ArgumentException("Invalid file format. File does not start with valid PDF signature (%PDF).");
            }
        }

        // 2. Load registration & student
        var registration = await _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);

        if (registration == null)
            throw new KeyNotFoundException($"Registration with ID {registrationId} not found.");

        var studentId = registration.StudentId;
        var cert = registration.Certificate;
        if (cert == null)
        {
            cert = new Certificate
            {
                CertificateId = Guid.NewGuid(),
                RegistrationId = registrationId,
                VerifiedStatus = CertificateStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Certificates.Add(cert);
        }

        // 3. Ensure bucket exists and upload to private cloud storage
        await _storageService.EnsurePrivateBucketExistsAsync(BUCKET_NAME, cancellationToken);

        var objectPath = $"{studentId:N}/{registrationId:N}/certificate.pdf";
        await _storageService.UploadCertificateAsync(BUCKET_NAME, objectPath, fileStream, "application/pdf", cancellationToken);

        // Verify existence
        var exists = await _storageService.ObjectExistsAsync(BUCKET_NAME, objectPath, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Failed to verify certificate object in private cloud storage after upload.");
        }

        // 4. Update DB entity with storage path only (never binary)
        cert.StoragePath = $"{BUCKET_NAME}/{objectPath}";
        cert.SubmittedDate = DateTime.UtcNow;
        cert.VerifiedStatus = CertificateStatus.Submitted;
        cert.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "CertificateUploaded",
            $"Uploaded certificate for student '{registration.Student?.RegisterNumber}' ({registration.Student?.Name}) in course '{registration.Course?.CourseCode}' to cloud object path '{cert.StoragePath}'",
            ipAddress,
            cancellationToken);

        return new AdminCertificateDto
        {
            CertificateId = cert.CertificateId,
            RegistrationId = registrationId,
            StudentId = studentId,
            StudentName = registration.Student?.Name ?? string.Empty,
            RegisterNumber = registration.Student?.RegisterNumber ?? string.Empty,
            Department = registration.Student?.Department ?? string.Empty,
            Year = registration.Student?.Year ?? 0,
            ClassSection = registration.Student?.ClassSection,
            CourseName = registration.Course?.CourseName ?? string.Empty,
            CourseCode = registration.Course?.CourseCode ?? string.Empty,
            StoragePath = cert.StoragePath,
            VerifiedStatus = cert.VerifiedStatus.ToString(),
            SubmittedDate = cert.SubmittedDate,
            VerifiedDate = cert.VerifiedDate,
            ReceivedDate = cert.ReceivedDate,
            IssuedDate = cert.IssuedDate
        };
    }

    public async Task<AdminCertificateDto> UpdateCertificateStatusAsync(
        Guid registrationId, 
        UpdateCertificateStatusDto dto, 
        Guid? adminUserId, 
        string? ipAddress, 
        CancellationToken cancellationToken = default)
    {
        var cert = await _context.Certificates
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Student)
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Course)
            .FirstOrDefaultAsync(c => c.RegistrationId == registrationId, cancellationToken);

        if (cert == null)
            throw new KeyNotFoundException($"Certificate for registration ID {registrationId} not found.");

        if (!Enum.TryParse<CertificateStatus>(dto.VerifiedStatus.Trim(), true, out var newStatus))
            throw new ArgumentException($"Invalid certificate status '{dto.VerifiedStatus}'. Allowed: Pending, Submitted, UnderVerification, Verified, Received.");

        var oldStatus = cert.VerifiedStatus;
        cert.VerifiedStatus = newStatus;

        if (newStatus == CertificateStatus.Verified)
        {
            cert.VerifiedDate = dto.VerifiedDate ?? DateTime.UtcNow;
        }
        else if (newStatus == CertificateStatus.Received)
        {
            cert.ReceivedDate = DateTime.UtcNow;
        }

        cert.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogActionAsync(
            adminUserId,
            "CertificateStatusUpdated",
            $"Updated certificate status from '{oldStatus}' to '{newStatus}' for student '{cert.Registration?.Student?.RegisterNumber}' in course '{cert.Registration?.Course?.CourseCode}'",
            ipAddress,
            cancellationToken);

        return new AdminCertificateDto
        {
            CertificateId = cert.CertificateId,
            RegistrationId = registrationId,
            StudentId = cert.Registration?.StudentId ?? Guid.Empty,
            StudentName = cert.Registration?.Student?.Name ?? string.Empty,
            RegisterNumber = cert.Registration?.Student?.RegisterNumber ?? string.Empty,
            Department = cert.Registration?.Student?.Department ?? string.Empty,
            Year = cert.Registration?.Student?.Year ?? 0,
            ClassSection = cert.Registration?.Student?.ClassSection,
            CourseName = cert.Registration?.Course?.CourseName ?? string.Empty,
            CourseCode = cert.Registration?.Course?.CourseCode ?? string.Empty,
            StoragePath = cert.StoragePath,
            VerifiedStatus = cert.VerifiedStatus.ToString(),
            SubmittedDate = cert.SubmittedDate,
            VerifiedDate = cert.VerifiedDate,
            ReceivedDate = cert.ReceivedDate,
            IssuedDate = cert.IssuedDate
        };
    }

    public async Task<CertificateAccessResponseDto> GetCertificateAccessAsync(
        Guid certificateId, 
        Guid requesterUserId, 
        string requesterRole, 
        CancellationToken cancellationToken = default)
    {
        var cert = await _context.Certificates
            .Include(c => c.Registration)
                .ThenInclude(r => r!.Student)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId, cancellationToken);

        if (cert == null)
            throw new KeyNotFoundException($"Certificate with ID {certificateId} not found.");

        if (string.IsNullOrWhiteSpace(cert.StoragePath))
            throw new InvalidOperationException("This certificate does not have an uploaded file yet.");

        // Authorization check:
        // 1. Admin: full access
        // 2. Student: must own the certificate
        // 3. Staff: must be within assigned scope
        if (requesterRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Allowed
        }
        else if (requesterRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            if (cert.Registration?.Student?.UserId != requesterUserId)
            {
                throw new UnauthorizedAccessException("Forbidden: Students can access only their own certificates.");
            }
        }
        else if (requesterRole.Equals("Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (cert.Registration?.StudentId == null)
            {
                throw new UnauthorizedAccessException("Forbidden: Certificate has no associated student.");
            }

            var canAccess = await _staffAuthService.CanStaffAccessStudentAsync(requesterUserId, cert.Registration.StudentId, cancellationToken);
            if (!canAccess)
            {
                throw new UnauthorizedAccessException("Forbidden: Staff can access only certificates of students in their assigned scope.");
            }
        }
        else
        {
            throw new UnauthorizedAccessException("Forbidden: Role not authorized to access certificates.");
        }

        // Parse bucket name and object path
        var rawPath = cert.StoragePath.TrimStart('/');
        var parts = rawPath.Split('/', 2);
        var bucket = parts.Length > 1 ? parts[0] : BUCKET_NAME;
        var objPath = parts.Length > 1 ? parts[1] : parts[0];

        // 15-minute signed URL
        var signedUrl = await _storageService.CreateSignedUrlAsync(bucket, objPath, 900, cancellationToken);

        return new CertificateAccessResponseDto
        {
            CertificateId = cert.CertificateId,
            AccessUrl = signedUrl,
            ExpiresAt = DateTime.UtcNow.AddSeconds(900),
            StoragePath = cert.StoragePath,
            StorageProvider = _storageService.ProviderName
        };
    }
}
