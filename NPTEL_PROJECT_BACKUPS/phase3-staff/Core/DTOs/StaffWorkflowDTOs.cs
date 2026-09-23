namespace NPTELManagement.Core.DTOs;

public class StaffDashboardSummaryDto
{
    public int TotalStudents { get; set; }
    public int RegisteredStudents { get; set; }
    public int InProgressCourses { get; set; }
    public int CompletedCourses { get; set; }
    public int ExamPending { get; set; }
    public int ExamApplied { get; set; }
    public int ExamCompleted { get; set; }
    public int CertificatePending { get; set; }
    public int CertificateSubmitted { get; set; }
    public int CertificateVerified { get; set; }
    public int CertificateReceived { get; set; }
}

public class StaffStudentListDto
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

    // NPTEL Summary within Scope
    public int RegisteredCourseCount { get; set; }
    public int InProgressCount { get; set; }
    public int ExamPending { get; set; }
    public int CertificatePending { get; set; }
    public string ExamStatusSummary { get; set; } = "NotStarted";
    public string CertificateStatusSummary { get; set; } = "Pending";
}

public class StaffStudentFilterDto
{
    public string? Search { get; set; }
    public int? Year { get; set; }
    public string? ClassSection { get; set; }
    public string? Course { get; set; }
    public string? RegistrationStatus { get; set; }
    public string? ExamStatus { get; set; }
    public string? CertificateStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class StaffStudentDetailsDto
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

    public List<StaffStudentCourseDto> Courses { get; set; } = new();
}

public class StaffStudentCourseDto
{
    public Guid RegistrationId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public DateTime RegistrationDate { get; set; }
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CurrentTimelineStatus { get; set; }

    public StudentExamDto? Exam { get; set; }
    public StudentCertificateDto? Certificate { get; set; }
}

public class StaffReportItemDto
{
    public string StudentName { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ClassSection { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string ExamStatus { get; set; } = string.Empty;
    public string CertificateStatus { get; set; } = string.Empty;
}

public class StaffReportPreviewDto
{
    public string ReportTitle { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ClassSection { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalRecords { get; set; }
    public List<StaffReportItemDto> Items { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
