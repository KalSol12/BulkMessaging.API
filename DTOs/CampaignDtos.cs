using BulkMessaging.API.Models;

namespace BulkMessaging.API.DTOs;

// ---------- Requests ----------

public class CreateCampaignDto
{
    public MessageChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
}

// ---------- Responses ----------

public class CampaignSummaryDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public MessageChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public CampaignStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
}

public class CampaignDetailDto : CampaignSummaryDto
{
    public List<MessageDto> Messages { get; set; } = new();
}

public class MessageDto
{
    public int Id { get; set; }
    public int? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Body { get; set; } = string.Empty;
    public MessageStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}