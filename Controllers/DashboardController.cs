using BulkMessaging.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace BulkMessaging.API.Controllers;

[ApiController]
[Authorize(Policy = "PasswordChanged")]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var now = DateTime.UtcNow;
        var startOfToday = now.Date;
        var startOfWeek = startOfToday.AddDays(-(int)now.DayOfWeek);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Total contacts (subscribers)
        var totalContacts = await _context.Contacts.CountAsync();
        var contactsToday = await _context.Contacts.CountAsync(c => c.CreatedAt >= startOfToday);
        var contactsThisWeek = await _context.Contacts.CountAsync(c => c.CreatedAt >= startOfWeek);
        var contactsThisMonth = await _context.Contacts.CountAsync(c => c.CreatedAt >= startOfMonth);

        // Total groups
        var totalGroups = await _context.Groups.CountAsync();

        // Total campaigns
        var totalCampaigns = await _context.Campaigns.CountAsync();

        // Messages stats
        var totalMessages = await _context.Messages.CountAsync();
        var sentMessages = await _context.Messages.CountAsync(m => m.Status == Models.MessageStatus.Sent);
        var failedMessages = await _context.Messages.CountAsync(m => m.Status == Models.MessageStatus.Failed);
        var sentMessagesToday = await _context.Messages.CountAsync(m =>
            m.Status == Models.MessageStatus.Sent && m.SentAt >= startOfToday);
        var sentMessagesThisWeek = await _context.Messages.CountAsync(m =>
            m.Status == Models.MessageStatus.Sent && m.SentAt >= startOfWeek);
        var sentMessagesThisMonth = await _context.Messages.CountAsync(m =>
            m.Status == Models.MessageStatus.Sent && m.SentAt >= startOfMonth);

        var topGroups = await _context.Groups
            .Select(g => new
            {
                id = g.Id,
                name = g.Name,
                contacts = g.Contacts.Count(),
                messagesSent = g.Contacts
                    .SelectMany(c => _context.Messages
                        .Where(m => m.ContactId == c.Id && m.Status == Models.MessageStatus.Sent))
                    .Count(),
            })
            .OrderByDescending(g => g.contacts)
            .ThenByDescending(g => g.messagesSent)
            .Take(5)
            .ToListAsync();

        var recentCampaigns = await _context.Campaigns
            .Where(c => c.Status == Models.CampaignStatus.Completed)
            .OrderByDescending(c => c.CompletedAt ?? c.CreatedAt)
            .Take(5)
            .Select(c => new
            {
                type = "campaign",
                title = c.Group != null ? c.Group.Name : "Campaign",
                detail = $"{c.SentCount} messages sent",
                date = c.CompletedAt ?? c.CreatedAt,
            })
            .ToListAsync();

        var recentGroups = await _context.Groups
            .OrderByDescending(g => g.CreatedAt)
            .Take(5)
            .Select(g => new
            {
                type = "group",
                title = g.Name,
                detail = "Group created",
                date = g.CreatedAt,
            })
            .ToListAsync();

        var recentContacts = await _context.Contacts
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new
            {
                type = "contact",
                title = c.Name ?? c.Phone,
                detail = "Contact added",
                date = c.CreatedAt,
            })
            .ToListAsync();

        var recentActivity = recentCampaigns
            .Concat(recentGroups)
            .Concat(recentContacts)
            .OrderByDescending(item => item.date)
            .Take(8)
            .ToList();

        return Ok(new
        {
            subscribers = new
            {
                total = totalContacts,
                thisMonth = contactsThisMonth,
                thisWeek = contactsThisWeek,
                today = contactsToday,
            },
            // Placeholder for future unsubscribe feature
            unsubscribers = new
            {
                total = 0,
                thisMonth = 0,
                thisWeek = 0,
                today = 0,
            },
            groups = totalGroups,
            campaigns = totalCampaigns,
            messages = new
            {
                total = totalMessages,
                sent = sentMessages,
                failed = failedMessages,
                thisMonth = sentMessagesThisMonth,
                thisWeek = sentMessagesThisWeek,
                today = sentMessagesToday,
            },
            topGroups,
            recentActivity,
        });
    }
}