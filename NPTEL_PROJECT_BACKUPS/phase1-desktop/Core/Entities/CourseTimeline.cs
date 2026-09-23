namespace NPTELManagement.Core.Entities;

public class CourseTimeline
{
    public Guid TimelineId { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public int? WeekNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public NptelRegistration? Registration { get; set; }
}
