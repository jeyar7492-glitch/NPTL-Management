namespace NPTELManagement.Core.Entities;

public class ClassEntity
{
    public Guid ClassId { get; set; } = Guid.NewGuid();
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Section { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
