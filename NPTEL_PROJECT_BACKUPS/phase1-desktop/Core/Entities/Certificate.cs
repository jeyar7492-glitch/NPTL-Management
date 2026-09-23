using NPTELManagement.Core.Enums;

namespace NPTELManagement.Core.Entities;

public class Certificate
{
    public Guid CertificateId { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public string? StoragePath { get; set; }
    public DateTime? IssuedDate { get; set; }
    public CertificateStatus VerifiedStatus { get; set; } = CertificateStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public NptelRegistration? Registration { get; set; }
}
