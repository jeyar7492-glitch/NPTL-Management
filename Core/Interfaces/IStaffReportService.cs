using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Core.Interfaces;

public interface IStaffReportService
{
    Task<StaffReportPreviewDto?> GetReportPreviewAsync(
        Guid staffUserId,
        string reportType,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateCsvAsync(
        Guid staffUserId,
        string reportType,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateXlsxAsync(
        Guid staffUserId,
        string reportType,
        CancellationToken cancellationToken = default);
}
