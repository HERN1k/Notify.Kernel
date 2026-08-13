using Notify.Core.Enums;
using Notify.Core.Models;

namespace Notify.Core.Abstractions
{
    public interface INotificationProvider
    {
        MessageProvider Channel { get; }
        Task<bool> SendAsync(IEnumerable<NotificationItem> notifications, CancellationToken cancellationToken = default);
    }
}
