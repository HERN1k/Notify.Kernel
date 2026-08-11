using Notify.Core.Enums;

namespace Notify.Core.Abstractions
{
    public interface INotificationProvider
    {
        MessageProvider Channel { get; }
        Task<bool> SendAsync(List<string> recipients, string message, CancellationToken cancellationToken = default);
    }
}
