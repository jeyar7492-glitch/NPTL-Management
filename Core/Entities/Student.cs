namespace NPTELManagement.Core.Entities;

public class Student
{
    public Guid StudentId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? ClassSection { get; set; }
    public int Year { get; set; }
    public int Semester { get; set; } = 1;
    public string? AcademicYear { get; set; } = "2026-27";
    public string? Batch { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public ICollection<NptelRegistration> Registrations { get; set; } = new List<NptelRegistration>();
}
