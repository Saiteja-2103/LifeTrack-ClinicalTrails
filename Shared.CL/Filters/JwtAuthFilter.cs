using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shared.CL.DTOs;

namespace Shared.CL.Filters;

/// <summary>
/// Global authorization filter that validates JWT Bearer tokens on every request.
/// Endpoints decorated with [AllowAnonymous] are skipped automatically.
///
/// Register once in Program.cs:
///   builder.Services.AddControllers(o => o.Filters.Add&lt;JwtAuthFilter&gt;());
///
/// This replaces AddAuthentication().AddJwtBearer() middleware — no UseAuthentication()
/// or UseAuthorization() calls are required.
/// </summary>
public class JwtAuthFilter : IAsyncAuthorizationFilter
{
    private readonly IConfiguration _config;

    public JwtAuthFilter(IConfiguration config) => _config = config;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Skip for endpoints that explicitly opt out of authentication.
        bool allowAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(em => em.GetType() == typeof(AllowAnonymousAttribute));
        if (allowAnonymous) return;

        string? authHeader = context.HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader)
            || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.Fail("Unauthorized. Missing or invalid token."))
            { StatusCode = 401 };
            return;
        }

        // Robust Bearer extraction — won't throw on edge cases like "Bearer" alone.
        string token = authHeader.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.Fail("Unauthorized. Empty bearer token."))
            { StatusCode = 401 };
            return;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            // Prefer JWT_SECRET environment variable for production deployments,
            // fall back to config (dev/local) so existing local runs still work.
            string secretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? _config["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

            string? issuer   = _config["Jwt:Issuer"];
            string? audience = _config["Jwt:Audience"];

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                                               Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer   = !string.IsNullOrWhiteSpace(issuer),
                ValidIssuer      = issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                ValidAudience    = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(
                token, validationParameters, out _);

            context.HttpContext.User = principal;
        }
        catch (Exception ex)
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.Fail(
                    "Unauthorized. Token expired or invalid. " + ex.Message))
            { StatusCode = 401 };
        }

        await Task.CompletedTask;
    }
}
