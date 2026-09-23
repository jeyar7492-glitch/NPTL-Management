using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Infrastructure.Services;

public class StaffReportService : IStaffReportService
{
    private readonly ApplicationDbContext _context;

    public StaffReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StaffReportPreviewDto?> GetReportPreviewAsync(
        Guid staffUserId, 
        string reportType, 
        CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == staffUserId || s.StaffId == staffUserId, cancellationToken);

        if (staff == null)
            return null;

        var studentQuery = _context.Students
            .AsNoTracking()
            .Where(s => s.Department.ToUpper() == staff.Department.ToUpper() && s.Year == staff.AssignedYear);

        if (!string.IsNullOrWhiteSpace(staff.AssignedClass))
        {
            studentQuery = studentQuery.Where(s => s.ClassSection != null && s.ClassSection.ToUpper() == staff.AssignedClass.ToUpper());
        }

        var students = await studentQuery
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Course)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.ExamStatus)
            .Include(s => s.Registrations)
                .ThenInclude(r => r.Certificate)
            .OrderBy(s => s.RegisterNumber)
            .ToListAsync(cancellationToken);

        var reportTitle = reportType.ToLower() switch
        {
            "student-registration" or "registration" or "student registration report" => "Student NPTEL Registration Report",
            "course-status" or "course" or "nptel course status report" => "NPTEL Course Progress Status Report",
            "exam-status" or "exam" or "exam status report" => "NPTEL Examination Status Report",
            "certificate-status" or "certificate" or "certificate status report" => "NPTEL Certificate Verification Report",
            _ => "NPTEL Comprehensive Academic Report"
        };

        var items = new List<StaffReportItemDto>();

        foreach (var student in students)
        {
            if (student.Registrations.Count == 0)
            {
                // Include student even if no registrations yet to show completeness in registration report
                if (reportType.ToLower().Contains("registration") || reportType.ToLower().Contains("comprehensive"))
                {
                    items.Add(new StaffReportItemDto
                    {
                        StudentName = student.Name,
                        RegisterNumber = student.RegisterNumber,
                        Department = student.Department,
                        Year = student.Year,
                        ClassSection = student.ClassSection,
                        CourseName = "Not Registered",
                        CourseCode = "—",
                        RegistrationStatus = "NotRegistered",
                        ExamStatus = "—",
                        CertificateStatus = "—"
                    });
                }
            }
            else
            {
                foreach (var reg in student.Registrations)
                {
                    var examSummary = reg.ExamStatus?.ExamApplicationStatus 
                        ?? reg.ExamStatus?.Status 
                        ?? "NotStarted";

                    var certSummary = reg.Certificate?.VerifiedStatus.ToString() ?? "Pending";

                    items.Add(new StaffReportItemDto
                    {
                        StudentName = student.Name,
                        RegisterNumber = student.RegisterNumber,
                        Department = student.Department,
                        Year = student.Year,
                        ClassSection = student.ClassSection,
                        CourseName = reg.Course?.CourseName ?? "—",
                        CourseCode = reg.Course?.CourseCode ?? "—",
                        RegistrationStatus = reg.Status.ToString(),
                        ExamStatus = examSummary,
                        CertificateStatus = certSummary
                    });
                }
            }
        }

        return new StaffReportPreviewDto
        {
            ReportTitle = reportTitle,
            ReportType = reportType,
            Department = staff.Department,
            Year = staff.AssignedYear,
            ClassSection = staff.AssignedClass,
            GeneratedAt = DateTime.UtcNow,
            TotalRecords = items.Count,
            Items = items
        };
    }
}
