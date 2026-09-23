namespace NPTELManagement.Core.DTOs;

public class StaffProfileResponse
{
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffIdentifier { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int AssignedYear { get; set; }
    public string? AssignedClass { get; set; }
}
