using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Authentication.API.DTOs;
using Authentication.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.CL.Enums;
using Shared.CL.Filters;

namespace Authentication.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserService _users;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService auth,
        IUserService users,
        IHttpClientFactory httpFactory,
        ILogger<AuthController> logger)
    {
        _auth = auth;
        _users = users;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    // Brute-force protection: 5 attempts / 5 min per source via "LoginPolicy"
    // (configured in Program.cs). Excess attempts get HTTP 429.
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        try { return Ok(await _auth.LoginAsync(req)); }
        catch (AuthException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    // ── Admin: create non-patient users ──────────────────────────────────────
    /// <summary>
    /// Admin-only. Creates any user role EXCEPT Patient (roleID 4).
    /// Patients must self-register via POST /api/auth/register/patient.
    /// </summary>
    [HttpPost("register")]
    [RoleAuthorize(RolesEnum.Admin)]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest req)
    {
        if (req.RoleID == (int)RolesEnum.Patient)
            return BadRequest(new { error = "Patients must self-register via /api/auth/register/patient." });

        try
        {
            var user = await _auth.RegisterAsync(req);
            _users.InvalidateListCache();
            return StatusCode(StatusCodes.Status201Created, user);
        }
        catch (AuthException ex) { return Conflict(new { error = ex.Message }); }
    }

    // ── Patient self-registration ─────────────────────────────────────────────
    /// <summary>
    /// Public endpoint. Creates a User account (role = Patient) AND a Patient record
    /// in Patient.API. Returns a JWT so the patient can use the app immediately.
    /// Uses a saga pattern: rolls back the user if the Patient record cannot be created.
    /// </summary>
    [HttpPost("register/patient")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterPatient([FromBody] PatientRegisterRequest req)
    {
        // ── Step 1: Create user account ───────────────────────────────────────
        UserResponse createdUser;
        try
        {
            var registerReq = new RegisterRequest
            {
                Name = req.Name,
                Email = req.Email,
                Password = req.Password,
                Phone = req.Phone,
                RoleID = (int)RolesEnum.Patient
            };
            createdUser = await _auth.RegisterAsync(registerReq);
        }
        catch (AuthException ex) { return Conflict(new { error = ex.Message }); }

        // ── Step 2: Auto-login to obtain the patient's JWT ────────────────────
        AuthResponse authResponse;
        try
        {
            authResponse = await _auth.LoginAsync(
                new LoginRequest { Email = req.Email, Password = req.Password });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-login failed after patient registration for {Email}. Rolling back.", req.Email);
            await TryDeleteUserAsync(createdUser.UserID);
            return StatusCode(500, new { error = "Account created but sign-in failed. Please contact support." });
        }

        // ── Step 3: Create the Patient record in Patient.API ──────────────────
        try
        {
            var client = _httpFactory.CreateClient("PatientApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authResponse.Token);

            var patientBody = new
            {
                userID = createdUser.UserID,
                name = req.Name,
                dob = req.DOB,
                contactInfo = req.Phone ?? req.Email,
                enrollmentStatus = "Screening"
            };

            var json = JsonSerializer.Serialize(patientBody,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("api/patients", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Patient API returned {Status} when creating patient record for UserID {ID}. Body: {Body}",
                    response.StatusCode, createdUser.UserID, body);
                await TryDeleteUserAsync(createdUser.UserID);
                return StatusCode(502, new { error = "Failed to create patient record. Please try registering again." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Patient API unreachable while registering UserID {ID}. Rolling back.", createdUser.UserID);
            await TryDeleteUserAsync(createdUser.UserID);
            return StatusCode(503, new { error = "Patient service is currently unavailable. Please try again later." });
        }

        // ── Step 4: Return token so the patient is immediately signed in ───────
        _users.InvalidateListCache();
        return StatusCode(StatusCodes.Status201Created, authResponse);
    }

    // ── Change password ───────────────────────────────────────────────────────
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var id = GetUserId();
        if (id is null) return Unauthorized();
        try { await _auth.ChangePasswordAsync(id.Value, req); return NoContent(); }
        catch (AuthException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private long? GetUserId()
    {
        var r = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(r, out var id) ? id : null;
    }

    /// <summary>Best-effort user deletion used as a compensating action on saga failure.</summary>
    private async Task TryDeleteUserAsync(long userId)
    {
        try { await _users.DeleteAsync(userId, userId); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Compensating delete failed for UserID {ID}. Manual cleanup may be required.", userId);
        }
    }
}
