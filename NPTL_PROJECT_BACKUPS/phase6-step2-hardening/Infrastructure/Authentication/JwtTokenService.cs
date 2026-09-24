using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Configuration;

namespace NPTELManagement.Infrastructure.Authentication;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Secret) || _options.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret is not configured or is too short (must be at least 32 characters).");
        }
    }

    public string GenerateToken(
        User user,
        string identifier,
        string name,
        Guid? roleSpecificId = null,
        string? department = null,
        int? assignedYear = null,
        string? assignedClass = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_options.Secret);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, name),
            new("username", user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role", user.Role.ToString()),
            new("identifier", identifier),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roleSpecificId.HasValue)
        {
            claims.Add(new Claim("role_id", roleSpecificId.Value.ToString()));
            if (user.Role == Core.Enums.UserRole.Student)
            {
                claims.Add(new Claim("student_id", roleSpecificId.Value.ToString()));
            }
            else if (user.Role == Core.Enums.UserRole.Staff)
            {
                claims.Add(new Claim("staff_id", roleSpecificId.Value.ToString()));
            }
            else if (user.Role == Core.Enums.UserRole.Admin)
            {
                claims.Add(new Claim("admin_id", roleSpecificId.Value.ToString()));
            }
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            claims.Add(new Claim("department", department));
        }

        if (assignedYear.HasValue)
        {
            claims.Add(new Claim("assigned_year", assignedYear.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(assignedClass))
        {
            claims.Add(new Claim("assigned_class", assignedClass));
        }

        var expires = GetTokenExpiration();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public DateTime GetTokenExpiration()
    {
        return DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
    }
}
