namespace NPTELManagement.Core.DTOs;

public class AdminProfileResponse
{
    public Guid AdminId { get; set; }
    public string AdminIdentifier { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int TotalStaff { get; set; }
    public int TotalCourses { get; set; }
    public int TotalRegistrations { get; set; }
    public int TotalCertificates { get; set; }
}
