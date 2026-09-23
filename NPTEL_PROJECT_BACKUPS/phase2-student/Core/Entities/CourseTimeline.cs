namespace NPTELManagement.Core.Entities;

public class CourseTimeline
{
    public Guid TimelineId { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty; // Completed, Current, Pending, Overdue
    public DateTime? EventDate { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public int? WeekNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public NptelRegistration? Registration { get; set; }
}
