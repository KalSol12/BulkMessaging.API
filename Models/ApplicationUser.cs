using Microsoft.AspNetCore.Identity;

namespace BulkMessaging.API.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}
