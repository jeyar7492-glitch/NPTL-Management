using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminCertificateService
{
    Task<PagedResult<AdminCertificateDto>> GetCertificatesAsync(string? search, string? verifiedStatus, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCertificateDto?> GetCertificateByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<AdminCertificateDto> UploadCertificateAsync(
        Guid registrationId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        Guid? adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default,
        string? certificateNumber = null,
        decimal? score = null,
        string? passStatus = null,
        DateTime? issuedDate = null);
    Task<AdminCertificateDto> UpdateCertificateStatusAsync(Guid registrationId, UpdateCertificateStatusDto dto, Guid? adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<CertificateAccessResponseDto> GetCertificateAccessAsync(Guid certificateId, Guid requesterUserId, string requesterRole, CancellationToken cancellationToken = default);
}
