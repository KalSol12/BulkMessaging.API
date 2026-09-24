using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace BulkMessaging.API.Services;

public class SmtpMessageSender : IMessageSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpMessageSender> _logger;

    public SmtpMessageSender(
        IOptions<SmtpSettings> settings,
        ILogger<SmtpMessageSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> SendAsync(
        string to,
        string? subject,
        string body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(to) || !to.Contains("@"))
        {
            throw new InvalidOperationException(
                $"Invalid email address: '{to}'");
        }

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            Credentials = new NetworkCredential(
                _settings.Username,
                _settings.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000, // 15s
        };

        // Generate a unique ID for this message
        var messageId = $"<{Guid.NewGuid():N}@{_settings.FromAddress.Split('@').LastOrDefault()}>";

        using var mail = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = subject ?? "(no subject)",
            Body = body,
            IsBodyHtml = body.Contains("<") && body.Contains(">"),  // crude HTML detection
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8,
        };

        mail.To.Add(to);
        mail.Headers.Add("Message-Id", messageId);

        try
        {
            await client.SendMailAsync(mail, ct);

            _logger.LogInformation(
                "[SMTP SENT] To: {To} | Subject: {Subject} | MessageId: {MessageId}",
                to,
                subject ?? "(none)",
                messageId);

            return messageId;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                ex,
                "[SMTP FAILED] To: {To} | StatusCode: {Code} | Error: {Message}",
                to,
                ex.StatusCode,
                ex.Message);

            // Give a cleaner message to the campaign log
            throw new InvalidOperationException(
                $"SMTP error ({ex.StatusCode}): {ex.Message}", ex);
        }
    }
}