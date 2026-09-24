using NPTELManagement.Core.Enums;

namespace NPTELManagement.Core.Entities;

public class NptelRegistration
{
    public Guid RegistrationId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Registered;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Student? Student { get; set; }
    public Course? Course { get; set; }
    public ICollection<CourseTimeline> Timeline { get; set; } = new List<CourseTimeline>();
    public ExamStatus? ExamStatus { get; set; }
    public Certificate? Certificate { get; set; }
}
