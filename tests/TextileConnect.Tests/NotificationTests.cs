using TextileConnect.Domain.Notifications;

namespace TextileConnect.Tests;

public sealed class NotificationTests
{
    [Fact]
    public void Notification_is_unread_until_marked_read()
    {
        var notification = new Notification { UserId = "u1", Title = "Order update", Message = "Your order shipped" };
        Assert.False(notification.IsRead);
        notification.IsRead = true;
        Assert.True(notification.IsRead);
    }
}
