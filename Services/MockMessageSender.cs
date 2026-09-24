namespace BulkMessaging.API.Services;

/// <summary>
/// Fake sender for development. Logs to console and returns a fake ID.
/// Occasionally fails, so you can test error handling.
/// </summary>
public class MockMessageSender : IMessageSender
{
    private readonly ILogger<MockMessageSender> _logger;
    private static int _counter = 0;

    public MockMessageSender(ILogger<MockMessageSender> logger)
    {
        _logger = logger;
    }

    public async Task<string> SendAsync(
        string to,
        string? subject,
        string body,
        CancellationToken ct = default)
    {
        // Simulate network delay
        await Task.Delay(50, ct);

        // Simulate occasional failure (about 15% of the time)
        var id = Interlocked.Increment(ref _counter);
        if (id % 7 == 0)
        {
            throw new Exception($"Mock failure for {to}");
        }

        var providerId = $"mock-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "[MOCK SEND] To: {To} | Subject: {Subject} | Body: {Body} | ProviderId: {ProviderId}",
            to,
            subject ?? "(none)",
            body,
            providerId);

        return providerId;
    }
}