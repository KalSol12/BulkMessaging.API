using System.ComponentModel.DataAnnotations;

namespace BulkMessaging.API.DTOs;

public sealed class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponseDto(
    string AccessToken,
    string Email,
    string DisplayName,
    bool IsAdmin,
    bool MustChangePassword);

public sealed record CurrentUserDto(
    string Email,
    string DisplayName,
    bool IsAdmin,
    bool MustChangePassword);

public sealed class ChangePasswordRequestDto
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class CreateUserRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    public string TemporaryPassword { get; init; } = string.Empty;
}

public sealed record UserSummaryDto(string Id, string Email, string DisplayName, bool IsAdmin);
