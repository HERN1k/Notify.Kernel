using Notify.Core.Abstractions;
using Notify.Core.Models;
using Notify.Helper;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Text;

namespace Notify.Infrastructure.Data
{
    public sealed class Repository : IRepository
    {
        private readonly IDbConnection _dbConnection;
        private DbConnection Connection { get; set; } = null!;

        public Repository(IDbConnection dbConnection)
        {
            this._dbConnection = dbConnection;
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken ct = default)
        {
            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM `customer` WHERE `customer_id` = @Id;";

                DbParameter param = cmd.CreateParameter();
                param.ParameterName = "@Id";
                param.Value = id;
                cmd.Parameters.Add(param);

                await using (DbDataReader reader = await cmd.ExecuteReaderAsync(ct))
                {
                    if (await reader.ReadAsync(ct))
                    {
                        return new CustomerDto(reader);
                    }

                    return null;
                }
            }
        }

        public async Task<IEnumerable<string>> GetEventTriggersAsync(string code, string action, CancellationToken ct = default) 
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(action);

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `trigger` FROM `event` WHERE `code` = @Code AND `action` = @Action ORDER BY `trigger` ASC;";

                DbParameter paramCode = cmd.CreateParameter();
                paramCode.ParameterName = "@Code";
                paramCode.Value = code;
                cmd.Parameters.Add(paramCode);

                DbParameter paramAction = cmd.CreateParameter();
                paramAction.ParameterName = "@Action";
                paramAction.Value = action;
                cmd.Parameters.Add(paramAction);

                await using (DbDataReader reader = await cmd.ExecuteReaderAsync(ct))
                {
                    List<string> result = new List<string>();

                    while (await reader.ReadAsync(ct)) 
                    {
                        string trigger = reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(trigger))
                        {
                            result.Add(trigger);
                        }
                    }

                    return result;
                }
            }
        }

        public async Task SyncEventsAsync(string code, string action, IEnumerable<string> triggers, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(action);
            ArgumentNullException.ThrowIfNull(triggers);

            await EnsureDBConnected(ct);

            List<string> triggerList = triggers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
            
            await using (DbTransaction transaction = await this.Connection.BeginTransactionAsync(ct)) 
            {
                try
                {
                    await using (DbCommand deleteCmd = this.Connection.CreateCommand())
                    {
                        deleteCmd.Transaction = transaction;
                        deleteCmd.CommandText = "DELETE FROM `event` WHERE `code` = @Code;";

                        DbParameter paramCode = deleteCmd.CreateParameter();
                        paramCode.ParameterName = "@Code";
                        paramCode.Value = code;
                        deleteCmd.Parameters.Add(paramCode);

                        await deleteCmd.ExecuteNonQueryAsync(ct);
                    }

                    if (triggerList.Count > 0)
                    {
                        await using (DbCommand insertCmd = this.Connection.CreateCommand())
                        {
                            insertCmd.Transaction = transaction;

                            DbParameter paramCode = insertCmd.CreateParameter();
                            paramCode.ParameterName = "@Code";
                            paramCode.Value = code;
                            insertCmd.Parameters.Add(paramCode);

                            DbParameter paramAction = insertCmd.CreateParameter();
                            paramAction.ParameterName = "@Action";
                            paramAction.Value = action;
                            insertCmd.Parameters.Add(paramAction);

                            DbParameter paramDate = insertCmd.CreateParameter();
                            paramDate.ParameterName = "@DateAdded";
                            paramDate.Value = DateTime.UtcNow;
                            insertCmd.Parameters.Add(paramDate);

                            List<string> valueSqls = new List<string>(triggerList.Count);

                            for (int i = 0; i < triggerList.Count; i++)
                            {
                                string paramName = $"@Trigger_{i}";
                                valueSqls.Add($"(@Code, {paramName}, @Action, 1, @DateAdded)");

                                DbParameter paramTrigger = insertCmd.CreateParameter();
                                paramTrigger.ParameterName = paramName;
                                paramTrigger.Value = triggerList[i];
                                insertCmd.Parameters.Add(paramTrigger);
                            }

                            insertCmd.CommandText = string.Concat("INSERT INTO `event` (`code`, `trigger`, `action`, `status`, `date_added`) VALUES ", string.Join(", ", valueSqls), ";");

                            await insertCmd.ExecuteNonQueryAsync(ct);
                        }
                    }

                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            }
        }

        public async Task<IEnumerable<NotificationQueueItem>> GetUninitializedTasksAsync(string name, CancellationToken ct = default) 
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM `notification_queue` WHERE `action` = @Action AND `attempts` = 0 AND (`send_after` IS NULL OR `send_after` = '0000-00-00 00:00:00' OR `send_after` = '');";

                DbParameter paramAction = cmd.CreateParameter();
                paramAction.ParameterName = "@Action";
                paramAction.Value = name;
                cmd.Parameters.Add(paramAction);

                await using (DbDataReader reader = await cmd.ExecuteReaderAsync(ct))
                {
                    List<NotificationQueueItem> result = new List<NotificationQueueItem>();

                    while (await reader.ReadAsync(ct))
                    {
                        result.Add(new NotificationQueueItem(reader));
                    }

                    return result;
                }
            }
        }

        public async Task BatchUpdateSendAfterAsync(IDictionary<long, DateTime> updates, CancellationToken ct = default)
        {
            if (updates.Count == 0) 
            {
                return;
            }

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                List<string> caseClauses = new List<string>(updates.Count);
                List<string> idParamNames = new List<string>(updates.Count);

                int index = 0;
                foreach (KeyValuePair<long, DateTime> kvp in updates)
                {
                    string idParamName = $"@id_{index}";
                    string dateParamName = $"@date_{index}";

                    caseClauses.Add($"WHEN {idParamName} THEN {dateParamName}");
                    idParamNames.Add(idParamName);

                    DbParameter paramId = cmd.CreateParameter();
                    paramId.ParameterName = idParamName;
                    paramId.Value = kvp.Key;
                    cmd.Parameters.Add(paramId);

                    DbParameter paramDate = cmd.CreateParameter();
                    paramDate.ParameterName = dateParamName;
                    paramDate.Value = kvp.Value;
                    cmd.Parameters.Add(paramDate);

                    index++;
                }

                cmd.CommandText = $@"
                    UPDATE `notification_queue`
                    SET `send_after` = CASE `notification_queue_id` 
                        {string.Join(" ", caseClauses)} 
                    END
                    WHERE `notification_queue_id` IN ({string.Join(", ", idParamNames)});";

                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task RecoverStuckTasksAsync(string name, DateTime fromDate, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                cmd.CommandText = $@"
                    UPDATE `notification_queue` SET 
                        `status` = 'pending' 
                    WHERE `action` = @Action
                        AND `status` = 'processing'
                        AND `date_modified` <= @Date;
                ";

                DbParameter paramAction = cmd.CreateParameter();
                paramAction.ParameterName = "@Action";
                paramAction.Value = name;
                cmd.Parameters.Add(paramAction);

                DbParameter paramDate = cmd.CreateParameter();
                paramDate.ParameterName = "@Date";
                paramDate.Value = fromDate;
                cmd.Parameters.Add(paramDate);
                
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task<IEnumerable<NotificationQueueItem>> GetPendingTasksAsync(string name, int maxAttempts, DateTime date, int batchLimit, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        nq.*, 
                        c.`firstname`, 
                        c.`lastname`, 
                        c.`email`, 
                        c.`language_id` 
                    FROM `notification_queue` nq 
                    LEFT JOIN `customer` c 
                        ON c.`customer_id` = nq.`customer_id` 
                       AND c.`customer_group_id` = 1 
                       AND c.`approved` = 1
                    WHERE nq.`action` = @Action 
                      AND nq.`status` = 'pending' 
                      AND nq.`attempts` < @MaxAttempts 
                      AND nq.`send_after` <= @Date
                    ORDER BY nq.`notification_queue_id` ASC 
                    LIMIT @Limit;";

                DbParameter paramAction = cmd.CreateParameter();
                paramAction.ParameterName = "@Action";
                paramAction.Value = name;
                cmd.Parameters.Add(paramAction);

                DbParameter paramMaxAttempts = cmd.CreateParameter();
                paramMaxAttempts.ParameterName = "@MaxAttempts";
                paramMaxAttempts.Value = maxAttempts;
                cmd.Parameters.Add(paramMaxAttempts);

                DbParameter paramDate = cmd.CreateParameter();
                paramDate.ParameterName = "@Date";
                paramDate.Value = date;
                cmd.Parameters.Add(paramDate);

                DbParameter paramLimit = cmd.CreateParameter();
                paramLimit.ParameterName = "@Limit";
                paramLimit.Value = batchLimit;
                cmd.Parameters.Add(paramLimit);

                await using (DbDataReader reader = await cmd.ExecuteReaderAsync(ct))
                {
                    List<NotificationQueueItem> result = new List<NotificationQueueItem>();

                    while (await reader.ReadAsync(ct))
                    {
                        result.Add(new NotificationQueueItem(reader));
                    }

                    return result;
                }
            }
        }

        public async Task<IReadOnlySet<int>> ClaimTasksAsync(string name, IEnumerable<int> taskIds, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            int[] idsList = taskIds.Distinct().ToArray();
            if (idsList.Length == 0)
            {
                return ImmutableHashSet<int>.Empty;
            }

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                string[] idParamNames = new string[idsList.Length];
                for (int i = 0; i < idsList.Length; i++)
                {
                    string paramName = $"@id_{i}";
                    idParamNames[i] = paramName;

                    DbParameter paramId = cmd.CreateParameter();
                    paramId.ParameterName = paramName;
                    paramId.Value = idsList[i];
                    cmd.Parameters.Add(paramId);
                }

                DbParameter paramAction = cmd.CreateParameter();
                paramAction.ParameterName = "@Action";
                paramAction.Value = name;
                cmd.Parameters.Add(paramAction);

                string inClause = string.Join(", ", idParamNames);

                cmd.CommandText = $@"
                    UPDATE `notification_queue`
                    SET `status` = 'processing', 
                        `attempts` = `attempts` + 1
                    WHERE `notification_queue_id` IN ({inClause})
                      AND `status` = 'pending'
                      AND `action` = @Action;

                    SELECT `notification_queue_id`
                    FROM `notification_queue`
                    WHERE `notification_queue_id` IN ({inClause})
                      AND `status` = 'processing'
                      AND `action` = @Action;";

                await using (DbDataReader reader = await cmd.ExecuteReaderAsync(ct))
                {
                    ImmutableHashSet<int>.Builder builder = ImmutableHashSet.CreateBuilder<int>();
                    
                    while (reader.FieldCount == 0 && await reader.NextResultAsync(ct)) { }

                    while (await reader.ReadAsync(ct))
                    {
                        builder.Add(reader.GetInt32(0));
                    }

                    return builder.ToImmutable();
                }
            }
        }

        public async Task CancelTaskAsync(int taskId, CancellationToken ct = default)
        {
            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                cmd.CommandText = " UPDATE `notification_queue` SET `status` = 'cancelled' WHERE `notification_queue_id` = @Id;";

                DbParameter paramId = cmd.CreateParameter();
                paramId.ParameterName = "@Id";
                paramId.Value = taskId;
                cmd.Parameters.Add(paramId);

                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task<int> ExecuteConditionQueryAsync(string sql, NotificationTask task, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return 0;
            }
            
            await EnsureDBConnected(ct);

            string processedSql = sql.Replace("{DB_PREFIX}", string.Empty);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                SQLMappingItem[] tokenMappings = new SQLMappingItem[]
                {
                    new SQLMappingItem("customer_id", "@CustomerId", (object?)task.CustomerId ?? DBNull.Value),
                    new SQLMappingItem("session_id", "@SessionId", task.SessionId),
                    new SQLMappingItem("telephone", "@Telephone", task.Telephone),
                    new SQLMappingItem("notification_queue_id", "@QueueId", task.Id),
                    new SQLMappingItem("id", "@QueueId", task.Id),
                    new SQLMappingItem("ip", "@Ip", task.Ip),
                    new SQLMappingItem("date_added", "@DateAdded", task.DateAdded),
                };

                foreach (SQLMappingItem mapping in tokenMappings)
                {
                    string rawToken = $"{{{mapping.Token}}}";
                    string quotedToken = $"'{rawToken}'";

                    if (processedSql.Contains(rawToken, StringComparison.OrdinalIgnoreCase))
                    {
                        processedSql = processedSql
                            .Replace(quotedToken, mapping.ParamName, StringComparison.OrdinalIgnoreCase)
                            .Replace(rawToken, mapping.ParamName, StringComparison.OrdinalIgnoreCase);

                        DbParameter param = cmd.CreateParameter();
                        param.ParameterName = mapping.ParamName;
                        param.Value = mapping.Value;
                        cmd.Parameters.Add(param);
                    }
                }

                cmd.CommandText = processedSql;
                
                object? result = await cmd.ExecuteScalarAsync(ct);

                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }

        public async Task BatchCompleteTasksAsync(IReadOnlyDictionary<int, string> completeTasks, CancellationToken ct = default)
        {
            if (completeTasks.Count == 0) 
            {
                return;
            }

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                string[] idParamNames = new string[completeTasks.Count];
                StringBuilder caseBuilder = new StringBuilder(completeTasks.Count * 80);

                int i = 0;
                foreach (KeyValuePair<int, string> kvp in completeTasks)
                {
                    string idParam = cmd.AddParam($"@Id_{i}", kvp.Key);
                    string payloadParam = cmd.AddParam($"@Payload_{i}", kvp.Value);

                    idParamNames[i] = idParam;

                    caseBuilder.Append(" WHEN ").Append(idParam).Append(" THEN ").Append(payloadParam);

                    i++;
                }

                cmd.CommandText = $@"
                    UPDATE `notification_queue` 
                    SET `status` = 'completed',
                        `payload` = CASE `notification_queue_id` {caseBuilder} END
                    WHERE `notification_queue_id` IN ({string.Join(", ", idParamNames)});
                ";

                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task BatchRescheduleTasksAsync(IReadOnlyDictionary<int, (DateTime SendAfter, string Payload)> rescheduleTasks, CancellationToken ct = default)
        {
            if (rescheduleTasks.Count == 0)
            {
                return;
            }

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                string[] idParamNames = new string[rescheduleTasks.Count];

                StringBuilder caseSendAfterBuilder = new StringBuilder(rescheduleTasks.Count * 80);
                StringBuilder casePayloadBuilder   = new StringBuilder(rescheduleTasks.Count * 80);

                int i = 0;
                foreach (KeyValuePair<int, (DateTime SendAfter, string Payload)> kvp in rescheduleTasks)
                {
                    string idParam = cmd.AddParam($"@Id_{i}", kvp.Key);
                    string sendAfterParam = cmd.AddParam($"@SendAfter_{i}", kvp.Value.SendAfter);
                    string payloadParam = cmd.AddParam($"@Payload_{i}", kvp.Value.Payload);

                    idParamNames[i] = idParam;

                    caseSendAfterBuilder.Append(" WHEN ").Append(idParam).Append(" THEN ").Append(sendAfterParam);
                    casePayloadBuilder.Append(" WHEN ").Append(idParam).Append(" THEN ").Append(payloadParam);

                    i++;
                }

                cmd.CommandText = $@"
                    UPDATE `notification_queue`
                    SET `status` = 'pending',
                        `send_after` = CASE `notification_queue_id` {caseSendAfterBuilder} END,
                        `payload` = CASE `notification_queue_id` {casePayloadBuilder} END
                    WHERE `notification_queue_id` IN ({string.Join(", ", idParamNames)});
                ";

                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task ResetTaskStatusAsync(IEnumerable<int> taskIds, string status = "pending", CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(status);

            int[] tasks = taskIds.ToArray();

            if (tasks.Length == 0) 
            {
                return;
            }

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                string[] parameterNames = new string[tasks.Length];

                for (int i = 0; i < tasks.Length; i++) 
                {
                    DbParameter paramId = cmd.CreateParameter();
                    paramId.ParameterName = parameterNames[i] = $"@Id_{i}"; ;
                    paramId.Value = tasks[i];
                    cmd.Parameters.Add(paramId);
                }

                DbParameter paramStatus = cmd.CreateParameter();
                paramStatus.ParameterName = "@Status";
                paramStatus.Value = status;
                cmd.Parameters.Add(paramStatus);

                cmd.CommandText = $"UPDATE `notification_queue` SET `status` = @Status WHERE `notification_queue_id` IN ({string.Join(',', parameterNames)});";

                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task LogSendBatchAsync(IEnumerable<NotificationSendLog> logItems, CancellationToken ct = default)
        {
            NotificationSendLog[] logs = logItems.ToArray();

            if (logs.Length == 0) 
            {
                return;
            }

            await EnsureDBConnected(ct);

            await using (DbCommand cmd = this.Connection.CreateCommand())
            {
                string[][] values = new string[logs.Length][];

                for (int i = 0; i < logs.Length; i++) 
                {
                    NotificationSendLog log = logs[i];

                    values[i] = new string[10];

                    values[i][0] = cmd.AddParam($"@QueueId_{i}", log.NotificationQueueId);
                    values[i][1] = cmd.AddParam($"@SessionId_{i}", log.SessionId);
                    values[i][2] = cmd.AddParam($"@CustomerId_{i}", (object?)log.CustomerId ?? DBNull.Value);
                    values[i][3] = cmd.AddParam($"@Telephone_{i}", log.Telephone);
                    values[i][4] = cmd.AddParam($"@Action_{i}", log.Action);
                    values[i][5] = cmd.AddParam($"@AttemptNumber_{i}", log.AttemptNumber);
                    values[i][6] = cmd.AddParam($"@Payload_{i}", log.Payload);
                    values[i][7] = cmd.AddParam($"@Status_{i}", log.Status);
                    values[i][8] = cmd.AddParam($"@ErrorMessage_{i}", (object?)log.ErrorMessage ?? DBNull.Value);
                    values[i][9] = cmd.AddParam($"@ScheduledAt_{i}", log.ScheduledAt);
                }

                int estimatedCapacity = logs.Length * 180;
                StringBuilder sb = new StringBuilder(estimatedCapacity);

                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append('(');

                    string[] row = values[i];
                    for (int j = 0; j < row.Length; j++)
                    {
                        if (j > 0)
                        {
                            sb.Append(", ");
                        }

                        sb.Append(row[j]);
                    }

                    sb.Append(')');
                }

                cmd.CommandText = $@"
                    INSERT IGNORE INTO `notification_send_log` (
                        `notification_queue_id`,
                        `session_id`,
                        `customer_id`,
                        `telephone`,
                        `action`,
                        `attempt_number`,
                        `payload`,
                        `status`,
                        `error_message`,
                        `scheduled_at`
                    ) VALUES {sb};";
                
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task EnsureDBConnected(CancellationToken ct = default)
        {
            if (this._dbConnection is not DbConnection dbConnection)
            {
                throw new InvalidOperationException("Connection must inherit from DbConnection");
            }

            if (dbConnection.State != ConnectionState.Open)
            {
                await dbConnection.OpenAsync(ct);
            }

            this.Connection = dbConnection;
        }
    }
}