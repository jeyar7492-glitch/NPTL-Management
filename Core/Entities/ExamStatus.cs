namespace NPTELManagement.Core.Entities;

public class ExamStatus
{
    public Guid ExamStatusId { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public DateTime? ExamDate { get; set; }
    public string? HallTicketStatus { get; set; }
    public decimal? Score { get; set; }
    public string? PassStatus { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public NptelRegistration? Registration { get; set; }
}
