namespace NPTELManagement.Core.Entities;

public class Staff
{
    public Guid StaffId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffIdentifier { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int AssignedYear { get; set; }
    public string? AssignedClass { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
}
