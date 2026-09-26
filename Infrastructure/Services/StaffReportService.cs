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
                        CertificateStatus = certSummary,
                        CertificateNumber = reg.Certificate?.CertificateNumber,
                        CertificateScore = reg.Certificate?.Score,
                        CertificatePassStatus = reg.Certificate?.PassStatus,
                        CertificateSubmittedDate = reg.Certificate?.SubmittedDate?.ToString("yyyy-MM-dd")
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
    public async Task<byte[]> GenerateCsvAsync(
        Guid staffUserId,
        string reportType,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportPreviewAsync(staffUserId, reportType, cancellationToken);
        if (report == null) throw new KeyNotFoundException("Staff context not found.");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("S.No,Student Name,Register Number,Department,Year,Class Section,Course Code,Course Name,Registration Status,Exam Status,Certificate Status,Certificate Number,Certificate Score,Certificate Pass Status,Certificate Submitted Date");

        for (var i = 0; i < report.Items.Count; i++)
        {
            var item = report.Items[i];
            sb.AppendLine(string.Join(",",
                i + 1,
                EscapeCsv(item.StudentName),
                EscapeCsv(item.RegisterNumber),
                EscapeCsv(item.Department),
                item.Year,
                EscapeCsv(item.ClassSection ?? ""),
                EscapeCsv(item.CourseCode),
                EscapeCsv(item.CourseName),
                EscapeCsv(item.RegistrationStatus),
                EscapeCsv(item.ExamStatus),
                EscapeCsv(item.CertificateStatus),
                EscapeCsv(item.CertificateNumber ?? ""),
                item.CertificateScore?.ToString("0.##") ?? "",
                EscapeCsv(item.CertificatePassStatus ?? ""),
                EscapeCsv(item.CertificateSubmittedDate ?? "")));
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> GenerateXlsxAsync(
        Guid staffUserId,
        string reportType,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportPreviewAsync(staffUserId, reportType, cancellationToken);
        if (report == null) throw new KeyNotFoundException("Staff context not found.");

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Staff NPTEL Details");

        var headers = new[]
        {
            "S.No","Student Name","Register Number","Department","Year","Class Section",
            "Course Code","Course Name","Registration Status","Exam Status","Certificate Status",
            "Certificate Number","Certificate Score","Certificate Pass Status","Certificate Submitted Date"
        };

        ws.Cell(1,1).Value = "NPTEL MANAGEMENT SYSTEM - STAFF BULK DETAILS";
        ws.Cell(1,1).Style.Font.Bold = true;
        ws.Cell(1,1).Style.Font.FontSize = 14;
        ws.Range(1,1,1,headers.Length).Merge();

        ws.Cell(2,1).Value = $"Scope: {report.Department} | Year {report.Year} | Class {report.ClassSection ?? "All"} | Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC";
        ws.Cell(2,1).Style.Font.Italic = true;
        ws.Range(2,1,2,headers.Length).Merge();

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(4,i+1).Value = headers[i];
            ws.Cell(4,i+1).Style.Font.Bold = true;
            ws.Cell(4,i+1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1E3A8A");
            ws.Cell(4,i+1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
        }

        for (var i = 0; i < report.Items.Count; i++)
        {
            var item = report.Items[i];
            var row = i + 5;
            var values = new object?[]
            {
                i + 1, item.StudentName, item.RegisterNumber, item.Department, item.Year,
                item.ClassSection ?? "", item.CourseCode, item.CourseName, item.RegistrationStatus,
                item.ExamStatus, item.CertificateStatus, item.CertificateNumber ?? "",
                item.CertificateScore.HasValue ? (double)item.CertificateScore.Value : null,
                item.CertificatePassStatus ?? "", item.CertificateSubmittedDate ?? ""
            };
            for (var col = 0; col < values.Length; col++)
            {
                ws.Cell(row, col + 1).Value = values[col]?.ToString() ?? string.Empty;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return $"\"{value}\"";
    }

}
