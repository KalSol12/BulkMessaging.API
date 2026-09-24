using System.Text.RegularExpressions;
using BulkMessaging.API.Models;

namespace BulkMessaging.API.Services;

public static class TemplateRenderer
{
    /// <summary>
    /// Replaces {Name}, {Phone}, {Email}, {Notes} placeholders in the template.
    /// Unknown placeholders are left as-is.
    /// </summary>
    public static string Render(string template, Contact contact)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return template
            .Replace("{Name}", contact.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Phone}", contact.Phone ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Email}", contact.Email ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Notes}", contact.Notes ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}