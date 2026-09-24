using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IAdminReportService
{
    Task<AdminReportPreviewDto> GetReportPreviewAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfReportAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateXlsxReportAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateCsvReportAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default);
}
