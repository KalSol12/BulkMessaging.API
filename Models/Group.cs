namespace BulkMessaging.API.Models;

public class Group
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}