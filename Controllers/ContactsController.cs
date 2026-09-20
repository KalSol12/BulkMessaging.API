using BulkMessaging.API.Data;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BulkMessaging.API.DTOs;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ClosedXML.Excel;
namespace BulkMessaging.API.Controllers;

[ApiController]
[Route("api")]
public class ContactsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ContactsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/groups/1/contacts
    [HttpGet("groups/{groupId}/contacts")]
    public async Task<ActionResult<IEnumerable<Contact>>> GetContactsByGroup(int groupId)
    {
        var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound($"Group {groupId} not found.");
        }

        var contacts = await _context.Contacts
            .Where(c => c.GroupId == groupId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(contacts);
    }

    // GET: api/contacts/5
    [HttpGet("contacts/{id}")]
    public async Task<ActionResult<Contact>> GetContact(int id)
    {
        var contact = await _context.Contacts.FindAsync(id);

        if (contact == null)
        {
            return NotFound();
        }

        return Ok(contact);
    }

    // POST: api/groups/1/contacts
    [HttpPost("groups/{groupId}/contacts")]
    public async Task<ActionResult<Contact>> CreateContact(int groupId, Contact contact)
    {
        var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound($"Group {groupId} not found.");
        }

        if (string.IsNullOrWhiteSpace(contact.Name))
        {
            return BadRequest("Contact name is required.");
        }

        if (string.IsNullOrWhiteSpace(contact.Phone))
        {
            return BadRequest("Contact phone is required.");
        }

        contact.Id = 0;
        contact.GroupId = groupId;
        contact.CreatedAt = DateTime.UtcNow;

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetContact),
            new { id = contact.Id },
            contact
        );
    }

    // PUT: api/contacts/5
    [HttpPut("contacts/{id}")]
    public async Task<IActionResult> UpdateContact(int id, Contact contact)
    {
        if (id != contact.Id)
        {
            return BadRequest("Contact ID does not match.");
        }

        var existing = await _context.Contacts.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.Name = contact.Name;
        existing.Phone = contact.Phone;
        existing.Email = contact.Email;
        existing.Notes = contact.Notes;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/contacts/5
    [HttpDelete("contacts/{id}")]
    public async Task<IActionResult> DeleteContact(int id)
    {
        var contact = await _context.Contacts.FindAsync(id);
        if (contact == null)
        {
            return NotFound();
        }

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync();

        return NoContent();
    }

       // POST: api/groups/1/contacts/import
    [HttpPost("groups/{groupId}/contacts/import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
    public async Task<ActionResult<ImportResultDto>> ImportContacts(
        int groupId,
        IFormFile file)
    {
        // 1. Validate group exists
        var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound($"Group {groupId} not found.");
        }

        // 2. Validate file
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            return BadRequest("Only .csv and .xlsx files are supported.");
        }

        var result = new ImportResultDto();
        var toInsert = new List<Contact>();
        List<ContactCsvRow> rows;

        // 3. Parse based on file type
        using (var stream = file.OpenReadStream())
        {
            try
            {
                rows = extension switch
                {
                    ".csv" => ParseCsv(stream),
                    ".xlsx" => ParseExcel(stream),
                    _ => throw new InvalidOperationException("Unsupported format.")
                };
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to parse file: {ex.Message}");
            }
        }

        result.TotalRows = rows.Count;

        // 4. Validate each row
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var lineNumber = i + 2; // +2: header is line 1, first data row is line 2

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                result.Errors.Add(new ImportErrorDto
                {
                    Line = lineNumber,
                    Message = "Name is required"
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Phone))
            {
                result.Errors.Add(new ImportErrorDto
                {
                    Line = lineNumber,
                    Message = "Phone is required"
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Email) &&
                !row.Email.Contains("@"))
            {
                result.Errors.Add(new ImportErrorDto
                {
                    Line = lineNumber,
                    Message = $"Invalid email: {row.Email}"
                });
                continue;
            }

            toInsert.Add(new Contact
            {
                Name = row.Name.Trim(),
                Phone = row.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim(),
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 5. Bulk insert valid rows
        if (toInsert.Count > 0)
        {
            _context.Contacts.AddRange(toInsert);
            await _context.SaveChangesAsync();
        }

        result.Imported = toInsert.Count;
        result.Failed = result.Errors.Count;

        return Ok(result);
    }

    // ---------- Helpers ----------

    private static List<ContactCsvRow> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        });

        return csv.GetRecords<ContactCsvRow>().ToList();
    }

    private static List<ContactCsvRow> ParseExcel(Stream stream)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var usedRange = sheet.RangeUsed();
        if (usedRange == null)
        {
            return new List<ContactCsvRow>();
        }

        var rows = usedRange.RowsUsed().ToList();
        if (rows.Count < 2)
        {
            return new List<ContactCsvRow>(); // only header, no data
        }

        // Map headers (case-insensitive, trimmed)
        var headerRow = rows[0];
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                headers[name] = cell.Address.ColumnNumber;
            }
        }

        int? GetColumn(params string[] names)
        {
            foreach (var n in names)
            {
                if (headers.TryGetValue(n, out var col))
                {
                    return col;
                }
            }
            return null;
        }

        var nameCol = GetColumn("name");
        var phoneCol = GetColumn("phone", "phonenumber", "phone number", "mobile");
        var emailCol = GetColumn("email", "e-mail");
        var notesCol = GetColumn("notes", "note", "comment");

        var result = new List<ContactCsvRow>();

        foreach (var row in rows.Skip(1))
        {
            string? Get(int? col) =>
                col.HasValue ? row.Cell(col.Value).GetString()?.Trim() : null;

            var contact = new ContactCsvRow
            {
                Name = Get(nameCol),
                Phone = Get(phoneCol),
                Email = Get(emailCol),
                Notes = Get(notesCol)
            };

            // Skip fully-empty rows
            if (string.IsNullOrWhiteSpace(contact.Name) &&
                string.IsNullOrWhiteSpace(contact.Phone) &&
                string.IsNullOrWhiteSpace(contact.Email) &&
                string.IsNullOrWhiteSpace(contact.Notes))
            {
                continue;
            }

            result.Add(contact);
        }

        return result;
    }
    }