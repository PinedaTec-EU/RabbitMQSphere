# DeadLetterRescue – User Guide

DeadLetterRescue is a lightweight RabbitMQ utility that continuously consumes messages from a dead-letter queue (DLQ), optionally re-publishes them to another exchange, and archives each payload to disk for later analysis.

---

## Prerequisites
- .NET 8.0 SDK or newer (for building/running the tool).
- Access to the RabbitMQ broker where the DLQ lives.
- File-system permissions to the output directory (when exporting messages).

---

## Command-Line Options

| Option | Required | Description |
| ------ | -------- | ----------- |
| `--dlq <queue>` | Yes | Name of the dead-letter queue to consume. |
| `--server <host>` | No (default `localhost`) | RabbitMQ host. |
| `--port <number>` | No (default `5672`) | RabbitMQ port. |
| `--vhost <vhost>` | No (default `/`) | Virtual host where the queue resides. |
| `--user <username>` | No (default `guest`) | Login username. |
| `--password <password>` | No (default `guest`) | Login password. |
| `--output <folder>` | No | Directory where rescued messages will be exported. Creates files named `<Ulid>.<MessageId>.<format>`. |
| `--format <json\|raw>` | No (default `json`) | Export format: `json` wraps metadata/body in JSON, `raw` writes the original payload bytes as UTF-8. |
| `--exchange <name>` | No | If provided, every rescued message is republished to this exchange using the original routing key. |

> Tip: combine `--output` and `--exchange` to both archive and replay the DLQ.

---

## Basic Workflow
1. **Start the tool** with the desired CLI options.
2. DeadLetterRescue connects to RabbitMQ and issues a `BasicConsume` on the specified DLQ.
3. For every delivery:
   - The payload is exported to disk (when `--output` is set).
   - The message is optionally republished to `--exchange`.
   - The original DLQ delivery is acknowledged (`BasicAck`).
4. The process keeps running until you stop it (Ctrl+C).

---

## Happy-Path Examples

Sample `deadletter.json` definition is not required, but here is how the file layout typically looks:

- Create an output folder (e.g., `C:\DLQExport`).
- Run the tool with CLI flags shown below.
- Each message generates two files: metadata + body (if `--format json`).

### 1. Archive DLQ messages to disk (JSON metadata)
```bash
dotnet DeadLetterRescue.dll \
  --dlq OfflineTag.DLQ \
  --server rabbitmq.dev.local \
  --user dlq_reader \
  --password S3cret! \
  --output C:\RescuedMessages \
  --format json
```
Outcome:
- Every DLQ message is written as `C:\RescuedMessages\<Ulid>.<MessageId>.json`.
- JSON file contains `MessageId`, `RoutingKey`, `Body`, and the original properties snapshot.
- DLQ keeps draining until you stop the process.
- Example metadata file:

```json
{
  "MessageId": "f8c520e6a9e04824908724060f5dd7a1",
  "RoutingKey": "Tag.OfflineTag",
  "Body": "{ \"TagId\": \"01HFW...\", \"status\": \"rejected\" }",
  "Properties": {
    "ContentType": "application/json",
    "Headers": {
      "x-message-type": "Tag.OfflineTag"
    }
  }
}
```

### 2. Replay DLQ messages to a recovery exchange
```bash
dotnet DeadLetterRescue.dll \
  --dlq Billing.DeadLetter \
  --exchange RecoveryExchange \
  --output /tmp/billing-dlq \
  --format raw
```
Outcome:
- Messages go back into `RecoveryExchange` with their original routing key, message id, and headers.
- Raw payload is saved under `/tmp/billing-dlq` using the same filename pattern.
- Use this to republish problematic messages after reviewing them.

---

## Error Scenarios & Troubleshooting

| Error | Possible Cause / Fix |
| ----- | -------------------- |
| `Error: The configuration must include a 'dlq' value.` | The `--dlq` flag was omitted. Supply the queue name. |
| `ACCESS_REFUSED - Login was refused using authentication mechanism PLAIN` | Wrong `--user` / `--password`, or the user lacks permissions for the vhost. Verify credentials and privileges. |
| `NOT_FOUND - no queue 'X' in vhost 'Y'` | Queue name or vhost is incorrect. Double-check `--dlq` and `--vhost`. |
| `System.IO.DirectoryNotFoundException` when exporting | The folder in `--output` does not exist and the process lacks rights to create it. Create the directory or grant permissions. |
| Files are written but republish never happens | You did not set `--exchange`, or the exchange name is wrong/missing on the broker. Add the flag or create the exchange. |
| Process never exits | By design the consumer runs indefinitely (awaits `Task.Delay(-1)`). Use Ctrl+C to terminate when done. |

---

## Operational Tips
- **Throttling:** If the DLQ is huge, consider running DeadLetterRescue alongside a monitoring script that pauses/resumes the consumer by revoking permissions or switching queues.
- **Disk Usage:** JSON exports can grow quickly. Clean up or move the folder periodically.
- **Idempotency:** Replay targets (`--exchange`) should be able to handle duplicates because DLQ contents may contain reprocessed messages.
- **Observability:** Wrap the process in systemd/Windows Service if you need automatic restarts.

---

## Cleaning Up
1. Stop the process with Ctrl+C.
2. Verify the DLQ depth in RabbitMQ Management UI.
3. Inspect the exported files or the replay destination exchange to ensure recovery succeeded.

DeadLetterRescue gives you a deterministic way to rescue, inspect, and re-drive poison messages without writing ad-hoc scripts each time. Happy rescuing!
