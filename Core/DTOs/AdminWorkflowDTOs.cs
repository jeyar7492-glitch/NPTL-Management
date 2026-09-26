namespace NPTELManagement.Core.DTOs;

#region 1. Admin Dashboard Metrics
public class AdminDashboardMetricsDto
{
    public Guid AdminId { get; set; }
    public string AdminIdentifier { get; set; } = string.Empty;

    // Student counts
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int InactiveStudents { get; set; }

    // Staff counts
    public int TotalStaff { get; set; }
    public int ActiveStaff { get; set; }
    public int InactiveStaff { get; set; }

    // Course counts
    public int TotalCourses { get; set; }
    public int ActiveCourses { get; set; }
    public int InactiveCourses { get; set; }

    // Registration counts
    public int TotalRegistrations { get; set; }
    public int RegisteredCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }

    // Exam counts
    public int ExamPendingCount { get; set; }
    public int ExamAppliedCount { get; set; }
    public int ExamCompletedCount { get; set; }

    // Certificate counts
    public int TotalCertificates { get; set; }
    public int CertificatesPendingCount { get; set; }
    public int CertificatesSubmittedCount { get; set; }
    public int CertificatesVerifiedCount { get; set; }
    public int CertificatesReceivedCount { get; set; }

    public int PendingCertificates { get; set; }
    public int VerifiedCertificates { get; set; }
    public int CompletedExams { get; set; }

    public List<AdminRegistrationDto> RecentRegistrations { get; set; } = new();
    public List<AuditLogDto> RecentAuditLogs { get; set; } = new();
}
#endregion

#region 2. Student CRUD DTOs
public class AdminStudentListDto
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? ClassSection { get; set; }
    public int Year { get; set; }
    public int Semester { get; set; }
    public string? AcademicYear { get; set; }
    public string? Batch { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public int RegisteredCoursesCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminStudentDetailDto
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? ClassSection { get; set; }
    public int Year { get; set; }
    public int Semester { get; set; }
    public string? AcademicYear { get; set; }
    public string? Batch { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AdminRegistrationDto> Registrations { get; set; } = new();
}

public class CreateStudentDto
{
    public string Name { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = "CSE";
    public string? ClassSection { get; set; } = "A";
    public int Year { get; set; } = 1;
    public int Semester { get; set; } = 1;
    public string AcademicYear { get; set; } = "2026-27";
    public string? Batch { get; set; } = "2024-2028";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? InitialPassword { get; set; }
}

public class UpdateStudentDto
{
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = "CSE";
    public string? ClassSection { get; set; }
    public int Year { get; set; }
    public int Semester { get; set; }
    public string? AcademicYear { get; set; }
    public string? Batch { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class UpdateEntityStatusDto
{
    public bool IsActive { get; set; }
}

public class UpdateStatusDto : UpdateEntityStatusDto
{
}
#endregion

#region 3. Staff CRUD DTOs
public class AdminStaffListDto
{
    public Guid StaffId { get; set; }
    public Guid UserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffIdentifier { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int AssignedYear { get; set; }
    public string? AssignedClass { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public int AssignedStudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminStaffDetailDto
{
    public Guid StaffId { get; set; }
    public Guid UserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffIdentifier { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int AssignedYear { get; set; }
    public string? AssignedClass { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public int AssignedStudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStaffDto
{
    public string StaffName { get; set; } = string.Empty;
    public string StaffIdentifier { get; set; } = string.Empty;
    public string Department { get; set; } = "CSE";
    public int AssignedYear { get; set; } = 1;
    public string? AssignedClass { get; set; } = "A";
    public string? Email { get; set; }
    public string? InitialPassword { get; set; }
}

public class UpdateStaffDto
{
    public string StaffName { get; set; } = string.Empty;
    public string Department { get; set; } = "CSE";
    public int AssignedYear { get; set; }
    public string? AssignedClass { get; set; }
    public string? Email { get; set; }
}

public class ResetStaffPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}
#endregion

#region 4. Course CRUD DTOs
public class AdminCourseDto
{
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public string Status { get; set; } = "Active";
    public int TotalRegistrations { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCourseDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; } = 12;
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public string Status { get; set; } = "Active";
}

public class UpdateCourseDto
{
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; } = 12;
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public string Status { get; set; } = "Active";
}

public class UpdateCourseStatusDto
{
    public string Status { get; set; } = "Active";
}
#endregion

#region 5. Registration CRUD DTOs
public class AdminRegistrationDto
{
    public Guid RegistrationId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ClassSection { get; set; }

    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }

    public DateTime EnrollmentDate { get; set; }
    public string Status { get; set; } = "Registered";

    // Sub-status
    public string ExamApplicationStatus { get; set; } = "NotStarted";
    public string CertificateStatus { get; set; } = "Pending";
}

public class CreateRegistrationDto
{
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime? EnrollmentDate { get; set; }
    public string Status { get; set; } = "Registered";
}

public class UpdateRegistrationDto
{
    public string Status { get; set; } = "Registered";
}
#endregion

#region 6. Exam Management DTOs
public class AdminExamDto
{
    public Guid ExamStatusId { get; set; }
    public Guid RegistrationId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;

    public string ExamApplicationStatus { get; set; } = "NotStarted";
    public DateTime? ExamApplicationDate { get; set; }
    public DateTime? ExamApplicationDeadline { get; set; }
    public DateTime? ExamDate { get; set; }
    public DateTime? LastResultReminderDate { get; set; }
    public string? HallTicketStatus { get; set; }
    public string ExamStatus { get; set; } = "NotStarted";
    public decimal? Score { get; set; }
    public string? PassStatus { get; set; }
}

public class UpdateAdminExamDto
{
    public string ExamApplicationStatus { get; set; } = "NotStarted";
    public DateTime? ExamApplicationDate { get; set; }
    public DateTime? ExamApplicationDeadline { get; set; }
    public DateTime? ExamDate { get; set; }
    public string? HallTicketStatus { get; set; }
    public string ExamStatus { get; set; } = "NotStarted";
    public decimal? Score { get; set; }
    public string? PassStatus { get; set; }
}
#endregion

#region 7. Certificate Management DTOs
public class AdminCertificateDto
{
    public Guid CertificateId { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ClassSection { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;

    public string? StoragePath { get; set; }
    public string? CertificateNumber { get; set; }
    public decimal? Score { get; set; }
    public string? PassStatus { get; set; }
    public bool HasFile => !string.IsNullOrWhiteSpace(StoragePath);
    public string VerifiedStatus { get; set; } = "Pending";
    public DateTime? SubmittedDate { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? IssuedDate { get; set; }
}

public class UpdateCertificateStatusDto
{
    public string VerifiedStatus { get; set; } = "Pending";
    public DateTime? VerifiedDate { get; set; }
}

public class CertificateAccessResponseDto
{
    public Guid CertificateId { get; set; }
    public string AccessUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = "SupabaseStorage";
}
#endregion

#region 8. Notification Management DTOs
public class AdminNotificationDto
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? RelatedRegistrationId { get; set; }
}

public class CreateNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetType { get; set; } = "Individual"; // Individual, Year, Class, AllDepartment
    public Guid? TargetUserId { get; set; }
    public string? Department { get; set; } = "CSE";
    public int? Year { get; set; }
    public string? ClassSection { get; set; }
    public Guid? RelatedRegistrationId { get; set; }
}
#endregion

#region 9. Report Management DTOs
public class AdminReportFilterDto
{
    public string ReportType { get; set; } = "student-registration"; // student-registration, course-status, exam-status, certificate-status, year-summary, class-summary
    public string? Department { get; set; }
    public int? Year { get; set; }
    public string? ClassSection { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
}

public class AdminReportPreviewDto
{
    public string ReportTitle { get; set; } = string.Empty;
    public string Department { get; set; } = "CSE";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalRecords { get; set; }
    public List<AdminReportItemDto> Items { get; set; } = new();
}

public class AdminReportItemDto
{
    public string StudentName { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ClassSection { get; set; }
    public string? Batch { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public string ExamStatus { get; set; } = string.Empty;
    public string CertificateStatus { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public decimal? Score { get; set; }
}
#endregion

#region 10. Audit Log DTOs
public class AuditLogDto
{
    public Guid LogId { get; set; }
    public Guid? UserId { get; set; }
    public string Username { get; set; } = "System";
    public string Role { get; set; } = "Admin";
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AuditLogFilterDto
{
    public string? Search { get; set; }
    public string? Action { get; set; }
    public string? Role { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
#endregion
