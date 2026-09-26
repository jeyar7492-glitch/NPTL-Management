namespace NPTELManagement.Core.DTOs;

public class StudentDashboardSummaryDto
{
    public int RegisteredCourses { get; set; }
    public int InProgressCourses { get; set; }
    public int CompletedCourses { get; set; }
    public int ExamPending { get; set; }
    public int ExamCompleted { get; set; }
    public int CertificatesPending { get; set; }
    public int CertificatesVerified { get; set; }
    public int CertificatesReceived { get; set; }
}

public class StudentCourseDto
{
    public Guid RegistrationId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
}

public class StudentTimelineItemDto
{
    public Guid TimelineId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Completed, Current, Pending, Overdue
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    public int DisplayOrder { get; set; }
}

public class StudentExamDto
{
    public string? ExamApplicationStatus { get; set; }
    public DateTime? ExamApplicationDate { get; set; }
    public DateTime? ExamApplicationDeadline { get; set; }
    public DateTime? ExamDate { get; set; }
    public string? HallTicketStatus { get; set; }
    public string? ExamStatus { get; set; }
    public decimal? Score { get; set; }
    public string? PassStatus { get; set; }
}

public class StudentCertificateDto
{
    public Guid CertificateId { get; set; }
    public string VerifiedStatus { get; set; } = string.Empty;
    public string? StoragePath { get; set; }
    public string? CertificateNumber { get; set; }
    public decimal? Score { get; set; }
    public string? PassStatus { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public bool ReminderEnabled { get; set; }
    public DateTime? ReminderDate { get; set; }
}

public class StudentCourseDetailsDto
{
    public Guid RegistrationId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public DateTime? CourseStartDate { get; set; }
    public DateTime? CourseEndDate { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;

    public StudentExamDto? Exam { get; set; }
    public StudentCertificateDto? Certificate { get; set; }
    public List<StudentTimelineItemDto> Timeline { get; set; } = new();
}

public class StudentCertificateReminderDto
{
    public bool Enabled { get; set; }
    public DateTime? ReminderDate { get; set; }
}

public class StudentCertificateReminderResponseDto
{
    public Guid RegistrationId { get; set; }
    public bool Enabled { get; set; }
    public DateTime? ReminderDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class StudentNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public Guid? RelatedRegistrationId { get; set; }
}

public class UpdateRegistrationStatusDto
{
    public NPTELManagement.Core.Enums.RegistrationStatus Status { get; set; }
}

public class UpdateExamDateDto
{
    public DateTime ExamDate { get; set; }
}
