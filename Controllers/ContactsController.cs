using BulkMessaging.API.Data;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

using BulkMessaging.API.DTOs;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ClosedXML.Excel;
namespace BulkMessaging.API.Controllers;
using BulkMessaging.API.Services;

[ApiController]
[Authorize(Policy = "PasswordChanged")]
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

        var normalizedPhone = PhoneNormalizer.Normalize(contact.Phone);

        // ✅ Duplicate check
        var duplicate = await _context.Contacts
            .AnyAsync(c => c.GroupId == groupId && c.Phone == normalizedPhone);

        if (duplicate)
        {
            return Conflict($"Phone {normalizedPhone} already exists in this group.");
        }

        contact.Id = 0;
        contact.GroupId = groupId;
        contact.Phone = normalizedPhone;
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
    [RequestSizeLimit(10 * 1024 * 1024)]
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
        List<ContactCsvRow> rows;

        // 3. Parse
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

        // 4. Load existing phones in this group (for duplicate check)
        var existingPhones = await _context.Contacts
            .Where(c => c.GroupId == groupId)
            .Select(c => c.Phone)
            .ToListAsync();

        var seenPhones = new HashSet<string>(
            existingPhones.Select(PhoneNormalizer.Normalize),
            StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<Contact>();

        // 5. Validate + normalize each row
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var lineNumber = i + 2;

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

            var normalizedPhone = PhoneNormalizer.Normalize(row.Phone);

            if (normalizedPhone.Length < 7)
            {
                result.Errors.Add(new ImportErrorDto
                {
                    Line = lineNumber,
                    Message = $"Invalid phone: {row.Phone}"
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

            // ✅ Duplicate check (case-insensitive on normalized phone)
            if (!seenPhones.Add(normalizedPhone))
            {
                result.Errors.Add(new ImportErrorDto
                {
                    Line = lineNumber,
                    Message = $"Duplicate phone in this group: {normalizedPhone}"
                });
                continue;
            }

            toInsert.Add(new Contact
            {
                Name = row.Name.Trim(),
                Phone = normalizedPhone,
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim(),
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 6. Bulk insert
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

    // Read entire content first
    var content = reader.ReadToEnd();
    var lines = content
        .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
        .Select(l => l.Trim())
        .Where(l => l.Length > 0)
        .ToList();

    if (lines.Count == 0)
    {
        return new List<ContactCsvRow>();
    }

    // Detect header: check if first line contains letters AND commas
    var firstLine = lines[0];
    var hasComma = firstLine.Contains(',');
    var hasLetters = firstLine.Any(char.IsLetter);

    // 🎯 Case 1: Proper CSV with header (has commas AND letters in first row)
    if (hasComma && hasLetters)
    {
        using var csvReader = new StringReader(content);
        using var csv = new CsvReader(csvReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        });

        return csv.GetRecords<ContactCsvRow>().ToList();
    }

    // 🎯 Case 2: Plain list of phone numbers (one per line)
    var result = new List<ContactCsvRow>();
    foreach (var line in lines)
    {
        // Skip if the line looks like a header (e.g., "phone" / "Phone Number")
        var lower = line.ToLowerInvariant();
        if (lower == "phone" || lower == "phonenumber" ||
            lower == "phone number" || lower == "mobile" || lower == "number")
        {
            continue;
        }

        // Remove quotes if any
        var phone = line.Trim('"', '\'', ' ');
        if (string.IsNullOrWhiteSpace(phone))
        {
            continue;
        }

        result.Add(new ContactCsvRow
        {
            Name = $"Contact {result.Count + 1}",   // Use phone as default name
            Phone = phone,
            Email = null,
            Notes = null
        });
    }

    return result;
}
    private static List<ContactCsvRow> ParseExcel(Stream stream)
{
    using var workbook = new XLWorkbook(stream);
    var sheet = workbook.Worksheets.First();

    var usedRange = sheet.RangeUsed();
    if (usedRange == null)
    {
        return new List<ContactCsvRow>();
    }

    var rows = usedRange.RowsUsed().ToList();
    if (rows.Count == 0)
    {
        return new List<ContactCsvRow>();
    }

    // ---------------------------------------------------------
    // 1. Detect if first row is a header row
    // ---------------------------------------------------------
    var firstRow = rows[0];
    var firstRowCells = firstRow.CellsUsed().ToList();

    bool IsHeaderCell(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToLowerInvariant();
        return t is "name" or "phone" or "phonenumber" or "phone number"
            or "mobile" or "email" or "e-mail" or "notes" or "note"
            or "comment" or "comments" or "number" or "contact";
    }

    var headerMatches = firstRowCells.Count(c => IsHeaderCell(c.GetString()));
    var hasHeader = headerMatches >= 1 && firstRowCells.Count > 1;

    // ---------------------------------------------------------
    // 2a. Headerless: single column of phone numbers (or any 1-col)
    // ---------------------------------------------------------
    if (!hasHeader && usedRange.ColumnCount() == 1)
    {
        return ParseExcelPhoneList(rows);
    }

    // ---------------------------------------------------------
    // 2b. Header-based parse (existing logic, but more forgiving)
    // ---------------------------------------------------------
    var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // If we don't detect a header, treat row 0 as data and use position-based mapping
    int dataStartIndex = 0;
    if (hasHeader)
    {
        foreach (var cell in firstRowCells)
        {
            var name = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                headers[name] = cell.Address.ColumnNumber;
            }
        }
        dataStartIndex = 1;
    }
    else
    {
        // No header: assume column order = name, phone, email, notes
        var cols = usedRange.Columns().ToList();
        if (cols.Count > 0) headers["name"] = cols[0].ColumnNumber();
        if (cols.Count > 1) headers["phone"] = cols[1].ColumnNumber();
        if (cols.Count > 2) headers["email"] = cols[2].ColumnNumber();
        if (cols.Count > 3) headers["notes"] = cols[3].ColumnNumber();
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

    var nameCol = GetColumn("name", "fullname", "full name", "contact");
    var phoneCol = GetColumn("phone", "phonenumber", "phone number", "mobile", "number");
    var emailCol = GetColumn("email", "e-mail");
    var notesCol = GetColumn("notes", "note", "comment", "comments");

    var result = new List<ContactCsvRow>();

    foreach (var row in rows.Skip(dataStartIndex))
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

        // If no name but phone exists → use phone as name
        if (string.IsNullOrWhiteSpace(contact.Name) &&
            !string.IsNullOrWhiteSpace(contact.Phone))
        {
            contact.Name = contact.Phone;
        }

        result.Add(contact);
    }

    return result;
}

// ---------------------------------------------------------
// Helper: single-column sheet of phone numbers
// ---------------------------------------------------------
private static List<ContactCsvRow> ParseExcelPhoneList(List<IXLRangeRow> rows)
{
    var result = new List<ContactCsvRow>();

    foreach (var row in rows)
    {
        var value = row.FirstCell()?.GetString()?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            continue;
        }

        // Skip header-looking rows
        var lower = value.ToLowerInvariant();
        if (lower is "phone" or "phonenumber" or "phone number"
            or "mobile" or "number" or "contact" or "contacts")
        {
            continue;
        }

        result.Add(new ContactCsvRow
        {
            Name = $"Contact {result.Count + 1}",   // use phone as default name
            Phone = value,
            Email = null,
            Notes = null
        });
    }

    return result;
}
    
    }