namespace NPTELManagement.Core.DTOs;

public class StudentSelfRegisterDto
{
    public string Name { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public int Year { get; set; } = 1;
    public int Semester { get; set; } = 1;
    public string Department { get; set; } = "CSE";
    public string ClassSection { get; set; } = "A";
    public string AcademicYear { get; set; } = "2026-27";
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
