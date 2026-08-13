using Notify.Core.Models;

namespace Notify.Core.Abstractions
{
    public interface IRepository
    {
        Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<string>> GetEventTriggersAsync(string code, string action, CancellationToken ct = default);
        Task SyncEventsAsync(string code, string action, IEnumerable<string> triggers, CancellationToken ct = default);
        Task<IEnumerable<NotificationQueueItem>> GetUninitializedTasksAsync(string name, CancellationToken ct = default);
        Task BatchUpdateSendAfterAsync(IDictionary<long, DateTime> updates, CancellationToken ct = default);
        Task RecoverStuckTasksAsync(string name, DateTime fromDate, CancellationToken ct = default);
        Task<IEnumerable<NotificationQueueItem>> GetPendingTasksAsync(string name, int maxAttempts, DateTime date, int batchLimit, CancellationToken ct = default);
        Task<IReadOnlySet<int>> ClaimTasksAsync(string name, IEnumerable<int> taskIds, CancellationToken ct = default);
        Task CancelTaskAsync(int taskId, CancellationToken ct = default);
        Task<int> ExecuteConditionQueryAsync(string sql, NotificationTask task, CancellationToken ct = default);
        Task BatchCompleteTasksAsync(IReadOnlyDictionary<int, string> completeTasks, CancellationToken ct = default);
        Task BatchRescheduleTasksAsync(IReadOnlyDictionary<int, (DateTime SendAfter, string Payload)> rescheduleTasks, CancellationToken ct = default);
        Task ResetTaskStatusAsync(IEnumerable<int> taskIds, string status = "pending", CancellationToken ct = default);
        Task LogSendBatchAsync(IEnumerable<NotificationSendLog> logItems, CancellationToken ct = default);
    }
}