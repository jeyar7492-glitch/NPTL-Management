using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NPTELManagement.Infrastructure.Services;

public class AdminReportService : IAdminReportService
{
    private readonly ApplicationDbContext _context;

    public AdminReportService(ApplicationDbContext context)
    {
        _context = context;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<AdminReportPreviewDto> GetReportPreviewAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var items = await QueryReportItemsAsync(filter, cancellationToken);

        var title = (filter.ReportType?.ToLower()) switch
        {
            "student-registration" or "registration" => "NPTEL Student Registration Report",
            "course-status" or "course" => "NPTEL Course Enrollment Status Report",
            "exam-status" or "exam" => "NPTEL Examination Schedule & Results Report",
            "certificate-status" or "certificate" => "NPTEL Certificate Verification Report",
            "year-summary" => "NPTEL Academic Year Summary Report",
            "class-summary" => "NPTEL Class Section Summary Report",
            _ => "NPTEL Comprehensive Academic Report"
        };

        return new AdminReportPreviewDto
        {
            ReportTitle = title,
            Department = string.IsNullOrWhiteSpace(filter.Department) ? "All Departments" : filter.Department,
            GeneratedAt = DateTime.UtcNow,
            TotalRecords = items.Count,
            Items = items
        };
    }

    public async Task<byte[]> GeneratePdfReportAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var preview = await GetReportPreviewAsync(filter, cancellationToken);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("NPTEL MANAGEMENT SYSTEM").Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                                c.Item().Text(preview.ReportTitle).Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"Department: {preview.Department} | Generated: {preview.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC | Total Records: {preview.TotalRecords}")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                        });
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });
                });

                page.Content().PaddingTop(10).Element(content =>
                {
                    content.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);  // S.No
                            columns.RelativeColumn(2);   // Student Name
                            columns.RelativeColumn(2);   // Reg No
                            columns.RelativeColumn(1);   // Dept/Yr/Sec
                            columns.RelativeColumn(3);   // Course
                            columns.RelativeColumn(1.5f);// Reg Status
                            columns.RelativeColumn(1.5f);// Exam Status
                            columns.ConstantColumn(45);  // Score
                            columns.RelativeColumn(1.5f);// Cert Status
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("#").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Student Name").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Register No").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Class").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Course").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Registration").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Exam").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Score").Bold().FontColor(Colors.White);
                            h.Cell().Background(Colors.Blue.Darken2).Padding(4).Text("Certificate").Bold().FontColor(Colors.White);
                        });

                        int idx = 1;
                        foreach (var item in preview.Items)
                        {
                            var bg = (idx % 2 == 0) ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(3).Text(idx.ToString());
                            table.Cell().Background(bg).Padding(3).Text(item.StudentName).Bold();
                            table.Cell().Background(bg).Padding(3).Text(item.RegisterNumber);
                            table.Cell().Background(bg).Padding(3).Text($"{item.Department} Y{item.Year} {item.ClassSection ?? ""}".Trim());
                            table.Cell().Background(bg).Padding(3).Text($"{item.CourseCode} - {item.CourseName}");
                            table.Cell().Background(bg).Padding(3).Text(item.RegistrationStatus);
                            table.Cell().Background(bg).Padding(3).Text(item.ExamStatus);
                            table.Cell().Background(bg).Padding(3).Text(item.Score.HasValue ? item.Score.Value.ToString("0.##") : "—");
                            table.Cell().Background(bg).Padding(3).Text(item.VerificationStatus);
                            idx++;
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateXlsxReportAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var preview = await GetReportPreviewAsync(filter, cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("NPTEL Report");

        // Title Block
        worksheet.Cell(1, 1).Value = "NPTEL MANAGEMENT SYSTEM - " + preview.ReportTitle.ToUpperInvariant();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, 10).Merge();

        worksheet.Cell(2, 1).Value = $"Department: {preview.Department} | Generated: {preview.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC | Total Records: {preview.TotalRecords}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 9;
        worksheet.Range(2, 1, 2, 10).Merge();

        // Header row
        string[] headers = 
        {
            "S.No", "Student Name", "Register Number", "Department", "Year", "Class/Sec", 
            "Course Code", "Course Name", "Duration", "Registration Status", "Exam Status", 
            "Score", "Certificate Status"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = worksheet.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data rows
        int row = 5;
        for (int i = 0; i < preview.Items.Count; i++)
        {
            var item = preview.Items[i];
            worksheet.Cell(row, 1).Value = i + 1;
            worksheet.Cell(row, 2).Value = item.StudentName;
            worksheet.Cell(row, 3).Value = item.RegisterNumber;
            worksheet.Cell(row, 4).Value = item.Department;
            worksheet.Cell(row, 5).Value = item.Year;
            worksheet.Cell(row, 6).Value = item.ClassSection ?? string.Empty;
            worksheet.Cell(row, 7).Value = item.CourseCode;
            worksheet.Cell(row, 8).Value = item.CourseName;
            worksheet.Cell(row, 9).Value = $"{item.DurationWeeks} Weeks";
            worksheet.Cell(row, 10).Value = item.RegistrationStatus;
            worksheet.Cell(row, 11).Value = item.ExamStatus;
            if (item.Score.HasValue)
            {
                worksheet.Cell(row, 12).Value = (double)item.Score.Value;
            }
            else
            {
                worksheet.Cell(row, 12).Value = "—";
            }
            worksheet.Cell(row, 13).Value = item.VerificationStatus;

            if (row % 2 == 0)
            {
                worksheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }

            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateCsvReportAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var preview = await GetReportPreviewAsync(filter, cancellationToken);

        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine("S.No,Student Name,Register Number,Department,Year,Class Section,Course Code,Course Name,Duration Weeks,Registration Status,Exam Status,Score,Certificate Status");

        int idx = 1;
        foreach (var item in preview.Items)
        {
            sb.AppendLine(string.Join(",",
                idx,
                EscapeCsv(item.StudentName),
                EscapeCsv(item.RegisterNumber),
                EscapeCsv(item.Department),
                item.Year,
                EscapeCsv(item.ClassSection ?? string.Empty),
                EscapeCsv(item.CourseCode),
                EscapeCsv(item.CourseName),
                item.DurationWeeks,
                EscapeCsv(item.RegistrationStatus),
                EscapeCsv(item.ExamStatus),
                item.Score.HasValue ? item.Score.Value.ToString("0.##") : "",
                EscapeCsv(item.VerificationStatus)
            ));
            idx++;
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<List<AdminReportItemDto>> QueryReportItemsAsync(AdminReportFilterDto filter, CancellationToken cancellationToken)
    {
        var query = _context.NptelRegistrations
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Include(r => r.ExamStatus)
            .Include(r => r.Certificate)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Department))
        {
            query = query.Where(r => r.Student != null && r.Student.Department.ToLower() == filter.Department.Trim().ToLower());
        }

        if (filter.Year.HasValue && filter.Year.Value > 0)
        {
            query = query.Where(r => r.Student != null && r.Student.Year == filter.Year.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ClassSection))
        {
            query = query.Where(r => r.Student != null && r.Student.ClassSection != null &&
                                     r.Student.ClassSection.ToLower() == filter.ClassSection.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim().ToLower();
            query = query.Where(r => r.Status.ToString().ToLower() == st);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(r =>
                (r.Student != null && (r.Student.Name.ToLower().Contains(s) || r.Student.RegisterNumber.ToLower().Contains(s))) ||
                (r.Course != null && (r.Course.CourseName.ToLower().Contains(s) || r.Course.CourseCode.ToLower().Contains(s))));
        }

        var list = await query
            .OrderBy(r => r.Student != null ? r.Student.Department : "")
            .ThenBy(r => r.Student != null ? r.Student.Year : 0)
            .ThenBy(r => r.Student != null ? r.Student.RegisterNumber : "")
            .ToListAsync(cancellationToken);

        return list.Select(r => new AdminReportItemDto
        {
            StudentName = r.Student?.Name ?? string.Empty,
            RegisterNumber = r.Student?.RegisterNumber ?? string.Empty,
            Department = r.Student?.Department ?? string.Empty,
            Year = r.Student?.Year ?? 0,
            ClassSection = r.Student?.ClassSection,
            Batch = r.Student?.Batch,
            CourseCode = r.Course?.CourseCode ?? string.Empty,
            CourseName = r.Course?.CourseName ?? string.Empty,
            DurationWeeks = r.Course?.DurationWeeks ?? 0,
            RegistrationStatus = r.Status.ToString(),
            ExamStatus = r.ExamStatus?.ExamApplicationStatus ?? r.ExamStatus?.Status ?? "NotStarted",
            CertificateStatus = r.Certificate?.VerifiedStatus.ToString() ?? "Pending",
            VerificationStatus = r.Certificate?.VerifiedStatus.ToString() ?? "Pending",
            Score = r.ExamStatus?.Score
        }).ToList();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return $"\"{value}\"";
    }
}
