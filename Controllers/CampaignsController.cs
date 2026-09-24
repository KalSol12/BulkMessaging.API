using BulkMessaging.API.Data;
using BulkMessaging.API.DTOs;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BulkMessaging.API.Services;
namespace BulkMessaging.API.Controllers;

[ApiController]
[Route("api")]
public class CampaignsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CampaignsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // --------------------------------------------------------
    // POST: api/groups/5/campaigns
    // --------------------------------------------------------
    [HttpPost("groups/{groupId}/campaigns")]
    public async Task<ActionResult<CampaignSummaryDto>> CreateCampaign(
        int groupId,
        CreateCampaignDto dto)
    {
        var group = await _context.Groups
            .Include(g => g.Contacts)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            return NotFound($"Group {groupId} not found.");
        }

        if (string.IsNullOrWhiteSpace(dto.Body))
        {
            return BadRequest("Message body is required.");
        }

        if (dto.Channel == MessageChannel.Email &&
            string.IsNullOrWhiteSpace(dto.Subject))
        {
            return BadRequest("Subject is required for email campaigns.");
        }

        var campaign = new Campaign
        {
            GroupId = groupId,
            Channel = dto.Channel,
            Subject = dto.Subject,
            Body = dto.Body,
            Status = CampaignStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            TotalCount = group.Contacts.Count,
            SentCount = 0,
            FailedCount = 0,
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCampaign),
            new { id = campaign.Id },
            ToSummary(campaign, group.Name));
    }

    // --------------------------------------------------------
    // GET: api/groups/5/campaigns
    // --------------------------------------------------------
    [HttpGet("groups/{groupId}/campaigns")]
    public async Task<ActionResult<IEnumerable<CampaignSummaryDto>>> GetCampaignsByGroup(
        int groupId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null)
        {
            return NotFound($"Group {groupId} not found.");
        }

        var campaigns = await _context.Campaigns
            .Where(c => c.GroupId == groupId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CampaignSummaryDto
            {
                Id = c.Id,
                GroupId = c.GroupId,
                GroupName = group.Name,
                Channel = c.Channel,
                Subject = c.Subject,
                Body = c.Body,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                StartedAt = c.StartedAt,
                CompletedAt = c.CompletedAt,
                TotalCount = c.TotalCount,
                SentCount = c.SentCount,
                FailedCount = c.FailedCount,
            })
            .ToListAsync();

        return Ok(campaigns);
    }

    // --------------------------------------------------------
    // GET: api/campaigns/5
    // --------------------------------------------------------
    [HttpGet("campaigns/{id}")]
    public async Task<ActionResult<CampaignDetailDto>> GetCampaign(int id)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Group)
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
        {
            return NotFound();
        }

        var detail = new CampaignDetailDto
        {
            Id = campaign.Id,
            GroupId = campaign.GroupId,
            GroupName = campaign.Group?.Name ?? string.Empty,
            Channel = campaign.Channel,
            Subject = campaign.Subject,
            Body = campaign.Body,
            Status = campaign.Status,
            CreatedAt = campaign.CreatedAt,
            StartedAt = campaign.StartedAt,
            CompletedAt = campaign.CompletedAt,
            TotalCount = campaign.TotalCount,
            SentCount = campaign.SentCount,
            FailedCount = campaign.FailedCount,
            Messages = campaign.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ContactId = m.ContactId,
                    ContactName = m.ContactName,
                    Phone = m.Phone,
                    Email = m.Email,
                    Body = m.Body,
                    Status = m.Status,
                    ProviderMessageId = m.ProviderMessageId,
                    Error = m.Error,
                    Attempts = m.Attempts,
                    CreatedAt = m.CreatedAt,
                    SentAt = m.SentAt,
                })
                .ToList(),
        };

        return Ok(detail);
    }

    // --------------------------------------------------------
    // GET: api/campaigns/5/messages
    // --------------------------------------------------------
    [HttpGet("campaigns/{id}/messages")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetCampaignMessages(int id)
    {
        var exists = await _context.Campaigns.AnyAsync(c => c.Id == id);
        if (!exists)
        {
            return NotFound();
        }

        var messages = await _context.Messages
            .Where(m => m.CampaignId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ContactId = m.ContactId,
                ContactName = m.ContactName,
                Phone = m.Phone,
                Email = m.Email,
                Body = m.Body,
                Status = m.Status,
                ProviderMessageId = m.ProviderMessageId,
                Error = m.Error,
                Attempts = m.Attempts,
                CreatedAt = m.CreatedAt,
                SentAt = m.SentAt,
            })
            .ToListAsync();

        return Ok(messages);
    }

    // --------------------------------------------------------
    // DELETE: api/campaigns/5
    // --------------------------------------------------------
    [HttpDelete("campaigns/{id}")]
    public async Task<IActionResult> DeleteCampaign(int id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
        {
            return NotFound();
        }

        if (campaign.Status == CampaignStatus.Sending)
        {
            return Conflict("Cannot delete a campaign that is currently sending.");
        }

        _context.Campaigns.Remove(campaign);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // --------------------------------------------------------
    // Helper
    // --------------------------------------------------------
    private static CampaignSummaryDto ToSummary(Campaign c, string groupName) => new()
    {
        Id = c.Id,
        GroupId = c.GroupId,
        GroupName = groupName,
        Channel = c.Channel,
        Subject = c.Subject,
        Body = c.Body,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        StartedAt = c.StartedAt,
        CompletedAt = c.CompletedAt,
        TotalCount = c.TotalCount,
        SentCount = c.SentCount,
        FailedCount = c.FailedCount,
    };
        // --------------------------------------------------------
    // GET: api/campaigns/5/preview?contactId=3
    // --------------------------------------------------------
    [HttpGet("campaigns/{id}/preview")]
    public async Task<ActionResult<object>> PreviewCampaign(
        int id,
        [FromQuery] int contactId)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
        {
            return NotFound();
        }

        var contact = await _context.Contacts.FindAsync(contactId);
        if (contact == null)
        {
            return NotFound($"Contact {contactId} not found.");
        }

        var rendered = TemplateRenderer.Render(campaign.Body, contact);

        return Ok(new
        {
            campaignId = campaign.Id,
            contactId = contact.Id,
            contactName = contact.Name,
            phone = contact.Phone,
            email = contact.Email,
            subject = campaign.Subject,
            body = rendered,
        });
    }


        // --------------------------------------------------------
    // POST: api/campaigns/5/send
    // --------------------------------------------------------
    [HttpPost("campaigns/{id}/send")]
    public async Task<ActionResult<CampaignSummaryDto>> SendCampaign(
        int id,
          [FromServices] MessageSenderDispatcher dispatcher)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Group)
                .ThenInclude(g => g!.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
        {
            return NotFound();
        }

        // Guard against double-send
        if (campaign.Status == CampaignStatus.Sending)
        {
            return Conflict("Campaign is already sending.");
        }
        if (campaign.Status == CampaignStatus.Completed)
        {
            return Conflict("Campaign has already been sent. Create a new one to resend.");
        }

        var contacts = campaign.Group?.Contacts.ToList() ?? new List<Contact>();

        if (contacts.Count == 0)
        {
            return BadRequest("The group has no contacts to send to.");
        }

        // Start
        campaign.Status = CampaignStatus.Sending;
        campaign.StartedAt = DateTime.UtcNow;
        campaign.TotalCount = contacts.Count;
        campaign.SentCount = 0;
        campaign.FailedCount = 0;

        // Create Pending messages
        var messages = new List<Message>();
        foreach (var contact in contacts)
        {
            if (campaign.Channel == MessageChannel.Sms &&
                string.IsNullOrWhiteSpace(contact.Phone))
            {
                continue;
            }
            if (campaign.Channel == MessageChannel.Email &&
                string.IsNullOrWhiteSpace(contact.Email))
            {
                continue;
            }

            var renderedBody = TemplateRenderer.Render(campaign.Body, contact);

            messages.Add(new Message
            {
                CampaignId = campaign.Id,
                ContactId = contact.Id,
                ContactName = contact.Name,
                Phone = contact.Phone,
                Email = contact.Email,
                Body = renderedBody,
                Status = MessageStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (messages.Count == 0)
        {
            campaign.Status = CampaignStatus.Failed;
            campaign.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return BadRequest("No contact has a valid destination for this channel.");
        }

        campaign.TotalCount = messages.Count;

        _context.Messages.AddRange(messages);
        await _context.SaveChangesAsync();

        // Send each
        int sent = 0;
        int failed = 0;

        foreach (var msg in messages)
        {
            try
            {
                var to = campaign.Channel == MessageChannel.Email
                    ? msg.Email!
                    : msg.Phone;

               var providerId = await dispatcher.SendAsync(
                    campaign.Channel,
                    to,
                    campaign.Subject,
                    msg.Body);

                msg.Status = MessageStatus.Sent;
                msg.ProviderMessageId = providerId;
                msg.SentAt = DateTime.UtcNow;
                msg.Attempts += 1;
                sent++;
            }
            catch (Exception ex)
            {
                msg.Status = MessageStatus.Failed;
                msg.Error = ex.Message;
                msg.Attempts += 1;
                failed++;
            }

            await _context.SaveChangesAsync();
        }

        // Finish
        campaign.SentCount = sent;
        campaign.FailedCount = failed;
        campaign.Status = failed == messages.Count
            ? CampaignStatus.Failed
            : CampaignStatus.Completed;
        campaign.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ToSummary(campaign, campaign.Group?.Name ?? string.Empty));
    }
}


