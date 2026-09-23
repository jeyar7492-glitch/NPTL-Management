namespace NPTELManagement.Core.Entities;

public class Admin
{
    public Guid AdminId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string AdminIdentifier { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
}
