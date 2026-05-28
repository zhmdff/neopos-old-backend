using BusinessLayer.ExternalServices.Abstractions;
using Domain.Common.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BusinessLayer.ExternalServices.Implementations;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GenerateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("CompanyId", user.CompanyId.ToString()),
            new Claim("UserFullName", user.FullName)
        };

        if (user.Role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, user.Role.NameEn));
            claims.Add(new Claim("IsAdmin", user.Role.IsAdmin.ToString().ToLower()));
        }

        if (user.LinkedAccountId.HasValue)
        {
            claims.Add(new Claim("LinkedAccountId", user.LinkedAccountId.Value.ToString()));
        }

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is missing.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        TimeZoneInfo bakuTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        DateTime bakuNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bakuTimeZone);

        var expireMinutes = double.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "43200");
        var expires = bakuNow.AddMinutes(expireMinutes);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public Task<string> GenerateWaiterSessionToken(Guid companyId, Guid cashShiftId, string? companyName)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, $"waiter:{cashShiftId}"),
            new Claim(ClaimTypes.Name, "Ofisiant"),
            new Claim(ClaimTypes.Role, "Waiter"),
            new Claim("CompanyId", companyId.ToString()),
            new Claim("CashShiftId", cashShiftId.ToString()),
            new Claim("IsWaiterSession", "true"),
            new Claim("UserFullName", "Ofisiant")
        };

        if (!string.IsNullOrEmpty(companyName))
            claims.Add(new Claim("CompanyName", companyName));

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is missing.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        TimeZoneInfo bakuTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        DateTime bakuNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bakuTimeZone);

        var expireMinutes = double.Parse(_configuration["Jwt:WaiterExpiresInMinutes"] ?? "720");
        var expires = bakuNow.AddMinutes(expireMinutes);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }
}