using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssetManagement.Application.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AssetManagement.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Create(int userId, string employeeNo, IEnumerable<string> permissionCodes, IEnumerable<string> roles)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("缺少 Jwt:Key 配置");
        var issuer = _configuration["Jwt:Issuer"] ?? "AssetManagement";
        var expireMinutes = int.TryParse(_configuration["Jwt:ExpireMinutes"], out var value)
            ? value
            : 120;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("employeeNo", employeeNo)
        };
        claims.AddRange(permissionCodes.Select(x => new Claim("perm", x)));
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

