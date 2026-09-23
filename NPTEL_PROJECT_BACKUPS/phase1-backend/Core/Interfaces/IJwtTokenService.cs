using NPTELManagement.Core.Entities;

namespace NPTELManagement.Core.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(
        User user,
        string identifier,
        string name,
        Guid? roleSpecificId = null,
        string? department = null,
        int? assignedYear = null,
        string? assignedClass = null);

    DateTime GetTokenExpiration();
}
