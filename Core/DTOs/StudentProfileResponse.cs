namespace NPTELManagement.Core.DTOs;

public class StudentProfileResponse
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? ClassSection { get; set; }
    public int Year { get; set; }
    public string? Batch { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
