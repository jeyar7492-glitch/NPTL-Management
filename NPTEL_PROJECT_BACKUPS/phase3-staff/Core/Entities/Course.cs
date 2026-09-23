namespace NPTELManagement.Core.Entities;

public class Course
{
    public Guid CourseId { get; set; } = Guid.NewGuid();
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<NptelRegistration> Registrations { get; set; } = new List<NptelRegistration>();
}
