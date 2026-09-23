namespace NPTELManagement.Core.DTOs;

public class AdminProfileResponse
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

    // Aggregate convenience properties for UI bindings
    public int PendingCertificates { get; set; }
    public int VerifiedCertificates { get; set; }
    public int CompletedExams { get; set; }
}
