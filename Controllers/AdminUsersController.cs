using BulkMessaging.API.DTOs;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BulkMessaging.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin", Policy = "PasswordChanged")]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminUsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserSummaryDto>>> GetUsers()
    {
        var users = await _userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();
        var summaries = new List<UserSummaryDto>(users.Count);
        foreach (var user in users)
        {
            summaries.Add(new UserSummaryDto(
                user.Id,
                user.Email!,
                GetDisplayName(user),
                await _userManager.IsInRoleAsync(user, "Admin")));
        }

        return Ok(summaries);
    }

    [HttpPost]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(CreateUserRequestDto request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            MustChangePassword = true,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };

        var createResult = await _userManager.CreateAsync(user, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                errors = createResult.Errors.Select(error => new
                {
                    error.Code,
                    error.Description,
                }),
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Unable to assign the default user role.",
            });
        }

        var response = new UserSummaryDto(user.Id, user.Email!, GetDisplayName(user), false);
        return CreatedAtAction(nameof(GetUsers), new { }, response);
    }

    private static string GetDisplayName(ApplicationUser user) =>
        string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email! : user.DisplayName;
}
