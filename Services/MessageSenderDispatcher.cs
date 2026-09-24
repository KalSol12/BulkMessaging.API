using BulkMessaging.API.Models;

namespace BulkMessaging.API.Services;

/// <summary>
/// Picks the right sender (SMS or Email) based on the channel.
/// </summary>
public class MessageSenderDispatcher
{
    private readonly SmsMessageSender _sms;
    private readonly SmtpMessageSender _email;

    public MessageSenderDispatcher(SmsMessageSender sms, SmtpMessageSender email)
    {
        _sms = sms;
        _email = email;
    }

    public Task<string> SendAsync(
        MessageChannel channel,
        string to,
        string? subject,
        string body,
        CancellationToken ct = default)
    {
        return channel switch
        {
            MessageChannel.Sms => _sms.SendAsync(to, subject, body, ct),
            MessageChannel.Email => _email.SendAsync(to, subject, body, ct),
            _ => throw new InvalidOperationException(
                $"Unsupported channel: {channel}")
        };
    }
}