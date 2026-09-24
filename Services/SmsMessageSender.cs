using AfroMessage;
using Microsoft.Extensions.Options;

namespace BulkMessaging.API.Services;

public class SmsMessageSender : IMessageSender
{
    private readonly IAfroMessageClient _afroClient;
    private readonly ILogger<SmsMessageSender> _logger;

    public SmsMessageSender(
        IAfroMessageClient afroClient,
        ILogger<SmsMessageSender> logger)
    {
        _afroClient = afroClient;
        _logger = logger;
    }

    public async Task<string> SendAsync(
        string to,
        string? subject,
        string body,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _afroClient.SendMessageAsync(to, body);

            // ✅ Explicit null checks to satisfy the compiler
            if (result is null)
            {
                throw new InvalidOperationException(
                    "AfroMessage returned a null result.");
            }

            if (result.IsFailure || result.Value is null)
            {
                // Error type probably overrides ToString() with code + message
                throw new InvalidOperationException(
                    $"AfroMessage error: {result.Error}");
            }

            // ✅ MessageId is Guid? — convert to string
            var messageId = result.Value.MessageId?.ToString()
                ?? $"afro-{Guid.NewGuid():N}";

            _logger.LogInformation(
                "[SMS SENT] To: {To} | MessageId: {MessageId}",
                to,
                messageId);

            return messageId;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "[SMS FAILED] To: {To}", to);
            throw new InvalidOperationException(
                $"AfroMessage send failed: {ex.Message}", ex);
        }
    }
}