namespace BulkMessaging.API.Models;

public class Contact
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key → Group
    public int GroupId { get; set; }

    // Navigation property
    public Group? Group { get; set; }
}