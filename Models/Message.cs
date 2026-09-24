namespace BulkMessaging.API.Models;

public enum MessageStatus
{
    Pending,
    Sent,
    Failed
}

public class Message
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public Campaign? Campaign { get; set; }

    public int? ContactId { get; set; }   // nullable — contact may be deleted later

    public Contact? Contact { get; set; }

    // Snapshot of destination
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ContactName { get; set; }

    // What was actually sent
    public string Body { get; set; } = string.Empty;

    // Delivery
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}