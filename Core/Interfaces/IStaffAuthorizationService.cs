namespace NPTELManagement.Core.Interfaces;

public interface IStaffAuthorizationService
{
    Task<bool> CanStaffAccessStudentAsync(Guid staffUserId, Guid studentId, CancellationToken cancellationToken = default);
    Task<bool> CanStaffAccessScopeAsync(Guid staffUserId, string department, int year, string? classSection, CancellationToken cancellationToken = default);
}
