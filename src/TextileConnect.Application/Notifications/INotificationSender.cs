namespace TextileConnect.Application.Notifications;

public interface INotificationSender
{
    Task SendAsync(string recipient, string templateKey, CancellationToken cancellationToken = default);
}

public sealed class DevelopmentNotificationSender : INotificationSender
{
    public Task SendAsync(string recipient, string templateKey, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Development notification {templateKey} -> {recipient}");
        return Task.CompletedTask;
    }
}
