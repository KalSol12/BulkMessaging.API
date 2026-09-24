namespace BulkMessaging.API.Services;

public interface IMessageSender
{
    /// <summary>
    /// Send a single message. Returns the provider's message ID on success.
    /// Throws on failure.
    /// </summary>
    Task<string> SendAsync(
        string to,       // phone or email
        string? subject, // subject (email only)
        string body,     // rendered body
        CancellationToken ct = default);
}