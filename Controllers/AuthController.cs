using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BulkMessaging.API.DTOs;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BulkMessaging.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserDto(
            user.Email!,
            GetDisplayName(user),
            await _userManager.IsInRoleAsync(user, "Admin"),
            user.MustChangePassword));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var mustChangePassword = user.MustChangePassword;
        user.MustChangePassword = false;
        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!result.Succeeded)
        {
            user.MustChangePassword = mustChangePassword;
            return BadRequest(new
            {
                errors = result.Errors.Select(error => new
                {
                    error.Code,
                    error.Description,
                }),
            });
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await _userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email!),
            new("AspNet.Identity.SecurityStamp", user.SecurityStamp!),
            new("mustChangePassword", user.MustChangePassword.ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

        var issuer = _configuration["Jwt:Issuer"]!;
        var audience = _configuration["Jwt:Audience"]!;
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:SigningKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("Jwt:AccessTokenMinutes"));
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new LoginResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            user.Email!,
            GetDisplayName(user),
            isAdmin,
            user.MustChangePassword));
    }

    private static string GetDisplayName(ApplicationUser user) =>
        string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email! : user.DisplayName;
}
