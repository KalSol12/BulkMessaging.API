namespace BulkMessaging.API.Models;

public enum CampaignStatus
{
    Draft,
    Sending,
    Completed,
    Failed,
    Cancelled
}

public enum MessageChannel
{
    Sms,
    Email
}

public class Campaign
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public Group? Group { get; set; }

    public MessageChannel Channel { get; set; }

    public string? Subject { get; set; }   // email only

    public string Body { get; set; } = string.Empty;

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Counters — quick to query in the list view
    public int TotalCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}