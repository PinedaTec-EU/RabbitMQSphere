# WatchQueueMessages – User Guide

WatchQueueMessages is a RabbitMQ helper that tails a queue in real time and writes each delivery to disk (payload + metadata) for debugging, auditing, or replay purposes.

---

## Prerequisites
- .NET 8.0 SDK or newer.
- Connectivity and credentials for the RabbitMQ broker.
- Write permissions in the output directory provided via `--output`.

---

## Command-Line Options

| Option | Required | Description |
| ------ | -------- | ----------- |
| `--queue <name>` | Yes | Queue to consume/monitor. |
| `--output <folder>` | Yes | Destination directory for exported messages (`<Ulid>.<format>` plus `<Ulid>.props.json`). |
| `--server <host>` | No (default `localhost`) | RabbitMQ host. |
| `--port <number>` | No (default `5672`) | RabbitMQ port. |
| `--vhost <vhost>` | No (default `/`) | Virtual host containing the queue. |
| `--user <username>` | No (default `guest`) | Login username. |
| `--password <password>` | No (default `guest`) | Login password. |
| `--format <json\|txt>` | No (default `json`) | File extension/content hint for payload output. |

Each consumed message is written twice:
1. `<Ulid>.props.json` – metadata (message id + RabbitMQ properties) with indentation.
2. `<Ulid>.<format>` – payload body encoded as UTF-8.

---

## Runtime Flow
1. Tool connects to RabbitMQ using the supplied options.
2. Ensures the output folder exists.
3. Starts an `AsyncEventingBasicConsumer` with manual acknowledgements.
4. For every delivery, saves metadata + body and acknowledges the message. On failure, the delivery is `BasicNack`’d with `requeue: true`.
5. Continues indefinitely until you press Ctrl+C.

---

## Happy-Path Examples

By default there is no definition file—everything is CLI-driven—but here is an example of the files produced for clarity:

- `output/01hf...props.json` (metadata):
```json
{
  "MessageId": "01HFZ3J1A6DYFZ6J0S5V7N7Z9C",
  "Properties": {
    "ContentType": "application/json",
    "CorrelationId": "31aa...",
    "Headers": {
      "x-message-type": "Tag.OfflineTag"
    }
  }
}
```
- `output/01hf....json` (payload):
```json
{
  "TagId": "01HFZ3J1A6DYFZ6J0S5V7N7Z9C",
  "status": "processed",
  "timestamp": "2024-01-17T11:31:22Z"
}
```

### 1. Capture JSON payloads from a queue
```bash
dotnet WatchQueueMessages.dll \
  --queue Tag.Offline.Events \
  --output C:\Queues\offline-Tag \
  --server rabbitmq.dev.local \
  --user inspector \
  --password S3cret! \
  --format json
```
Result:
- Files like `01hf3q1y6cyc5nhh0dte0d1b.props.json` and `01hf3q1y6cyc5nhh0dte0d1b.json` appear under `C:\Queues\offline-Tag`.
- The `.props.json` file includes the broker-generated properties, while the `.json` file contains the body exactly as received.
- Every message is acknowledged after successful write.

### 2. Quick text dump for troubleshooting
```bash
dotnet WatchQueueMessages.dll \
  --queue deadletter.audit \
  --output /tmp/dlq-dump \
  --format txt
```
Result:
- Payloads are stored with `.txt` suffix, useful when messages are plain text/CSV/etc.
- Metadata JSON companions remain available for correlation.

---

## Error Scenarios & Fixes

| Error | Possible Cause / Fix |
| ----- | -------------------- |
| `Uso: WatchQueueMessages ...` printed immediately | Mandatory `--queue` or `--output` missing. Provide both flags. |
| `ACCESS_REFUSED - Login was refused using PLAIN` | Invalid credentials or lack of permissions on the vhost. Update `--user`, `--password`, or grant rights. |
| `NOT_FOUND - no queue 'X' in vhost 'Y'` | Queue name/vhost typo. Check `--queue` and `--vhost`. |
| `System.IO.DirectoryNotFoundException` | Output folder path does not exist and process cannot create it. Ensure the parent directory exists or run with sufficient permissions. |
| `Error al procesar el mensaje: ...` in console | Writing the file failed (disk full, read-only path, invalid encoding). Message is requeued; fix the underlying issue before continuing. |
| Tool appears stuck and never exits | Expected behavior; it runs indefinitely. Press Ctrl+C when you are done monitoring. |

---

## Operational Tips
- **Disk hygiene:** Large queues can generate thousands of files; monitor disk usage and rotate directories.
- **Message replay:** Use the `.props.json` metadata later to reconstruct headers when resubmitting payloads.
- **Multiple queues:** Launch separate processes (or terminals) per queue for parallel captures.
- **Automation:** Wrap the command in scripts to timestamp folders (e.g., `--output logs/%DATE%/queue1`).

---

## Shutdown & Cleanup
1. Stop the watcher (Ctrl+C). Manual loop will end.
2. Verify that the queue depth decreases as expected in RabbitMQ Management UI.
3. Archive or delete captured files once investigated.

WatchQueueMessages makes it trivial to “tail” a RabbitMQ queue and persist every delivery for offline inspection without writing custom consumers each time.
