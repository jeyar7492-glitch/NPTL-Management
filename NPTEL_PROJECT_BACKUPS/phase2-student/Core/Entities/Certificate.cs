using NPTELManagement.Core.Enums;

namespace NPTELManagement.Core.Entities;

public class Certificate
{
    public Guid CertificateId { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public string? StoragePath { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public CertificateStatus VerifiedStatus { get; set; } = CertificateStatus.Pending; // Pending, Submitted, UnderVerification, Verified, Received
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public NptelRegistration? Registration { get; set; }
}
