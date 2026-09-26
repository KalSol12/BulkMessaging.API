using BulkMessaging.API.Data;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace BulkMessaging.API.Controllers;

[ApiController]
[Authorize(Policy = "PasswordChanged")]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public GroupsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/groups
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Group>>> GetGroups()
    {
        var groups = await _context.Groups
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.CreatedAt,
                ContactCount = g.Contacts.Count,
            })
            .ToListAsync();

        return Ok(groups);
    }

    // GET: api/groups/1
    [HttpGet("{id}")]
    public async Task<ActionResult<Group>> GetGroup(int id)
    {
        var group = await _context.Groups
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.CreatedAt,
                ContactCount = g.Contacts.Count,
            })
            .SingleOrDefaultAsync();

        if (group == null)
        {
            return NotFound();
        }

        return Ok(group);
    }

    // POST: api/groups
    [HttpPost]
    public async Task<ActionResult<Group>> CreateGroup(Group group)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
        {
            return BadRequest("Group name is required.");
        }

        group.Id = 0;
        group.CreatedAt = DateTime.UtcNow;

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetGroup),
            new { id = group.Id },
            group
        );
    }

    // PUT: api/groups/1
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGroup(int id, Group group)
    {
        if (id != group.Id)
        {
            return BadRequest("Group ID does not match.");
        }

        var existingGroup = await _context.Groups.FindAsync(id);

        if (existingGroup == null)
        {
            return NotFound();
        }

        existingGroup.Name = group.Name;
        existingGroup.Description = group.Description;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/groups/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var group = await _context.Groups.FindAsync(id);

        if (group == null)
        {
            return NotFound();
        }

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}