# SendMessageToExchange Ã¢â‚¬â€œ User Guide

SendMessageToExchange is a flexible publisher harness for RabbitMQ/MQTT that loads message templates, applies variables (including random generators and per-message context), and sends batches to a target exchange/topic. It also supports exporting rendered payloads to disk for auditing.

---

## Prerequisites
- .NET 8.0 SDK or newer.
- Definition JSON file describing payloads, variables, and scheduling (create your own using the examples below).
- Access to the target RabbitMQ or MQTT broker, plus credentials and permissions to publish.

---

## Command-Line Options

Run via:
```bash
dotnet SendMessageToExchange.dll --def <path/to/definition.json>
```

| Option         | Required | Description                                                  |
| -------------- | -------- | ------------------------------------------------------------ |
| `--def <file>` | Yes      | Definition JSON (payload templates, variables, count, etc.). |

All connectivity settings (protocol, server, port, credentials, vhost) live exclusively inside the definition file and fall back to the same defaults the CLI exposed previously.

---

## Definition File Anatomy

Minimal example definition (`definition.json`):
```json
{
  "protocol": "amqp",
  "server": "rabbitmq.dev.local",
  "port": 5672,
  "user": "publisher",
  "password": "Pa55!",
  "vhost": "/",
  "messageType": "GMV.ITS.Suite.QueueMessages.Tap.OfflineTap",
  "exchange": "TapProcessor.Commands",
  "routingKey": "GMV.ITS.Suite.QueueMessages.Tap.OfflineTap",
  "count": 2,
  "threads": 0,
  "formatting": {
    "date": "yyyy-MM-dd",
    "time": "HH:mm:ss",
    "datetime": "yyyy-MM-ddTHH:mm:ss.fffZ"
  },
  "variables": {
    "GlobalCorrelationId": { "type": "guid" }
  },
  "export": {
    "enabled": true,
    "template": "Exports/{{GlobalCorrelationId}}/payload.json"
  },
  "payloads": [
    {
      "path": "payloads/TapEvent.json",
      "count": 3,
      "variables": {
        "TapId": { "type": "ulid" },
        "TapSequence": { "type": "number", "min": 1000, "max": 9999 },
        "TapLabel": { "value": "offline" }
      }
    }
  ]
}
```

Top-level definition fields:

| Property              | Required | Default                        | Description                                                                                         |
| --------------------- | -------- | ------------------------------ | --------------------------------------------------------------------------------------------------- |
| `protocol`            | No       | `amqp`                         | `amqp` (RabbitMQ) or `mqtt`.                                                                        |
| `server`              | No       | `localhost`                    | Broker host/IP.                                                                                     |
| `user`                | No       | `guest`                        | Username for AMQP/MQTT authentication.                                                              |
| `password`            | No       | `guest`                        | Password for AMQP/MQTT authentication.                                                              |
| `port`                | No       | `5672` (AMQP) / `1883` (MQTT)  | Overrides the default protocol port.                                                                |
| `vhost`               | No       | `/`                            | RabbitMQ virtual host (ignored for MQTT).                                                           |
| `mqttProtocolVersion` | No       | `v310`                         | MQTT only: `v310`, `v311`, or `v500` to choose the protocol version.                                |
| `messageType`         | Yes      | —                              | Default message type when per-payload override is absent.                                           |
| `exchange`            | Yes      | —                              | Default exchange (AMQP). MQTT ignores this value and uses `routingKey` as topic.                    |
| `routingKey`          | No       | `messageType`                  | Default routing key (AMQP) or topic (MQTT).                                                         |
| `count`               | No       | `1`                            | Iterations of the payload list.                                                                     |
| `threads`             | No       | `0` (auto = CPU count)         | Degree of parallelism; `0` uses processor count; minimum 1.                                         |
| `variables`           | No       | —                              | Global variables available to every payload.                                                        |
| `formatting`          | No       | Built-in defaults              | Global date/time/datetime formats for random generators.                                            |
| `export`              | No       | Disabled                       | Global export defaults: `enabled`, `template`, `overwrite`. Paths are relative to the definition.   |
| `payloads`            | Yes      | —                              | Array of payload entries (see below).                                                               |

For MQTT connections the tool automatically prefixes the username with `"<vhost>:<user>"` when the virtual host is different from `/` and the configured user does not already include a colon. This matches the credential pattern expected by el plugin MQTT de RabbitMQ (`mqtt://<vhost>:<user>:<password>@host`). If you prefer to manage the prefix yourself, define the username exactly as the broker expects (e.g., `"user": "suite:test"`), and it will be used as-is.

Per-payload fields:

| Property      | Required | Default             | Description                                                                                       |
| ------------- | -------- | ------------------- | ------------------------------------------------------------------------------------------------- |
| `path`        | Yes      | —                   | Template file path (relative or absolute).                                                        |
| `count`       | No       | `1`                 | Duplicates this payload within each global iteration.                                             |
| `variables`   | No       | —                   | Variables merged on top of global `variables`.                                                    |
| `export`      | No       | Uses global export  | Per-payload export override (`enabled`, `template`, `overwrite`).                                 |
| `exchange`    | No       | Global `exchange`   | Per-payload override. Ignored for MQTT (warning if not `amq.topic`).                              |
| `routingKey`  | No       | Global `routingKey` | Per-payload override. Used as AMQP routing key or MQTT topic.                                     |
| `messageType` | No       | Global `messageType`| Per-payload override.                                                                             |

Key sections:
- `variables`: global tokens available to every payload. Supports literals or random generators (`number`, `text`, `guid`, `ulid`, `datetime`, `date`, `time`, `sequence`, `fixed`). Per-payload variables merge over globals.
- `payloads`: list of template entries; each must provide `path` and can override `count`, `variables`, `export`, `exchange`, `routingKey`, `messageType`.
- `formatting`: optional object with `date`, `time`, and `datetime` entries overriding the default `yyyy-MM-dd` / `HH:mm:ss` / `O` formats used by the random generators. Setting a `format` directly on a variable definition takes precedence over these global values.
- `export`: optional. When enabled, each rendered payload is written to disk using your template (no automatic suffixing). Example: `Exports/{{GlobalCorrelationId}}/{{context.templateFileNameStem}}-{{context.index:D4}}.json`. The export template supports the same variables and context tokens as payload bodies. You can also set `overwrite: true` to allow replacing existing files.
- Context tokens (always accessible): `{{context.index}}` resolves to the sequential message counter.

## Variables
### Formatting precedence
1. **Per-variable format**: Add `"format": "<custom>"` inside any `datetime`, `date` or `time` variable definition to force a specific output (for example, `"format": "yyyyMMddHHmmss"`). These are the types where custom output formats apply.
2. **Global formatting block**: Use the top-level `formatting` object to define defaults for every variable of that type (`date`, `time`, `datetime`). These defaults apply whenever an individual variable omits its own `format`.
3. **Built-in defaults**: If neither of the above is specified, the generator falls back to `yyyy-MM-dd`, `HH:mm:ss`, and the ISO 8601 `O` format respectively.

You can still apply ad-hoc formatting while templating (e.g., `{{EventDate:yyyy/MM/dd}}`), but those expressions work on top of the already-formatted string emitted by the generator.

### Variable Types & Defaults
Variables accept either a literal object (`{ "value": "foo" }` or `"foo"`) or a random generator with a `type`. Available types:

| Type       | Purpose                                        | Optional Properties                                 | Defaults / Notes                                                                                                                                           |
| ---------- | ---------------------------------------------- | --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `number`   | Integer randomizer.                            | `min`, `max` (inclusive), `padding` (digits).       | Defaults to `min=1`, `max=100`. Example output: `42`. Padding uses `D<padding>` (e.g., padding 4 turns `7` into `0007`).                                   |
| `sequence` | Deterministic counter tied to `context.index`. | `start` (default 1), `step` (default 1), `padding`. | Emits `start + (context.index - 1) * step`, e.g., `100`, `101`, `102`. Padding works like the `number` type to emit `00100`, `00101`, etc.                 |
| `text`     | Random alphanumeric string.                    | `length`.                                           | Default length is 16. Example output: `q4f9p1mx7bn2s3ty`. Minimum enforced length is 1.                                                                    |
| `guid`     | Generates `Guid.NewGuid()`.                    | _none_.                                             | Lowercase GUID string, e.g., `3f1c2c0f3a5e4c9ab7d8c6f1a2b3c4d5`.                                                                                           |
| `ulid`     | Generates `Ulid.NewUlid()`.                    | _none_.                                             | Lexicographically sortable ULID, e.g., `01JABCD2EFGHJKL3MNOPQRSTUV`.                                                                                       |
| `datetime` | Random `DateTimeOffset`.                       | `from`, `to`, `format`.                             | Defaults to `UtcNow - 1 month` to `UtcNow + 1 month`. ISO 8601 output such as `2025-10-22T09:46:46Z`. `format` overrides the global `formatting.datetime`. |
| `date`     | Random `DateOnly`.                             | `from`, `to`, `format`.                             | Defaults to a +/-1 month window around today. Example output: `2025-10-22`. `format` overrides the global `formatting.date`.                               |
| `time`     | Random `TimeOnly`.                             | `from`, `to`, `format`.                             | Defaults to `00:00:00` to `23:59:59.9999999`. Example output: `14:05:33`. `format` overrides the global `formatting.time`.                                 |
| `fixed`    | Explicit literal value using object syntax.    | `value`.                                            | Equivalent to writing a plain string; useful for schema consistency. Example output: `SampleTenant`.                                                       |

When both a global `formatting` entry and a per-variable `format` are provided, the per-variable value wins. You can still apply ad-hoc formatting in templates using the `{{Variable:format}}` syntax, which operates on the already-formatted string.

Literal variables can still embed other tokens (e.g., `"value": "order-{{context.index:D4}}"`), enabling derived names. Random definitions cache per message, so multiple template references share the same generated value within a payload.

Sequence example:

```json
"variables": {
  "InvoiceNumber": { "type": "sequence", "start": 1000, "padding": 4 },
  "BatchSequence": { "type": "sequence", "start": 5000, "step": 10 },
  "FixedTenant": { "type": "fixed", "value": "GMV-ITS" }
}
```

For the first few payloads you would get `InvoiceNumber = 1000, 1001, 1002...` and `BatchSequence = 5000, 5010, 5020...`, aligning with the global `context.index`. `FixedTenant` always resolves to `GMV-ITS`.

### Context Tokens
During rendering each payload receives a `PayloadProcessorContext`. The following properties are exposed via `{{context.*}}`:

| Token                                                                               | Description                                                                                                                                                                                                                            |
| ----------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `{{context.index}}`                                                                 | 1-based running counter across all scheduled messages (respecting repetitions and iterations). Use standard formatting (e.g., `{{context.index}}` inside `string.Format` style or suffix logic) to produce padded values such as `D4`. |
| `{{context.templateFilePath}}`                                                      | Absolute path of the template file currently being processed. Useful when export paths need to mirror the source structure.                                                                                                            |
| `{{context.templateFileName}}`                                                      | File name (with extension) derived from the template path, e.g., `TapEvent.json`.                                                                                                                                                      |
| `{{context.templateDirectory}}`                                                     | Directory component of the template path.                                                                                                                                                                                              |
| `{{context.templateFileNameWithoutExtension}}` / `{{context.templateFileNameStem}}` | File name without the extension, e.g., `TapEvent`.                                                                                                                                                                                     |

These context tokens are available everywhere templates are resolved payload bodies and export path templates so you can build file names such as `Exports/{{context.templateFileNameStem}}/{{context.index:D4}}.json`. Any identifier without the `context.` prefix is treated as a variable name. Every token accepts an optional `:format` suffix, so `{{InvoiceNumber:D6}}` or `{{context.index:D4}}` apply .NET-style numeric formatting without modifying the variable definition.

- Template example referenced above (`payloads/TapEvent.json`):

```json
{
  "messageId": "{{GlobalCorrelationId}}",
  "tapId": "{{TapId}}",
  "sequence": "{{TapSequence}}",
  "label": "{{TapLabel}}",
  "contextual": "idx-{{context.index}} file={{context.templateFileName}}"
}
```

Save the template next to the definition or update the `path` accordingly.

---

## Workflow Overview
1. CLI parses options and loads the definition file.
2. `PayloadBuilderProcessor` expands payload templates per iteration, evaluating variables and random values upfront when necessary.
3. `MessageProcessorBase` enqueues scheduled deliveries (`count` payload entries).
4. Depending on the `protocol` value in the definition (`amqp` or `mqtt`), either `AmqpMessageProcessor` or `MqttMessageProcessor` sends each payload:
   - AMQP: publishes to `exchange/routingKey` with persistent JSON body.
   - MQTT: publishes to the topic defined by `exchange` (or `routingKey` override) as UTF-8 text.
5. If export is enabled, every resolved payload is also written to disk alongside its send attempt.

### Graceful cancellation
Press `Ctrl+C` once to signal cancellation. The CLI propagates the cancellation token through all publishers (AMQP/MQTT) so in-flight sends finish cleanly; press `Ctrl+C` again if you need to force an immediate exit.

---

## Happy-Path Scenarios

### 1. AMQP burst with JSON exports
```bash
dotnet SendMessageToExchange.dll --def definition.json
```
Result:
- Multiple payloads generated via template variables and sent to the specified exchange/routing key using the connection info stored in the definition (`protocol: "amqp"`, `server`, credentials, etc.).
- Export files appear under the folder outlined in the sample (`Exports/.../TapEvent-0001.json`, etc.) based on your template expressions.
- Console output shows Ã¢â‚¬Å“OK <template name>Ã¢â‚¬Â per message and final statistics.

### 2. MQTT replay with custom iterations/threads
Configure your definition with `"protocol": "mqtt"`, the desired MQTT broker/port, and the `count`/`threads` values you want. Then run:
```bash
dotnet SendMessageToExchange.dll --def definition.json
```
Result:
- The configured iterations (`count`) and workers (`threads`) from the definition are honoured.
- Messages publish over MQTT to the topic defined by `exchange`/`routingKey`.
- Export artifacts follow whatever pattern is defined in `TelemetryReplay.json`.

---

## Error Scenarios & Remedies

| Error                                                   | Possible Cause / Fix                                                                                                     |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `The configuration must include a 'payloads' array.`    | Definition JSON lacks the `payloads` section or is malformed. Add an array of payload entries.                           |
| `Variable 'Foo' is not defined for payload 'Bar.json'.` | Template references an undefined token and no resolver is available. Define the variable globally or within the payload. |
| `Export template resolved to an empty path...`          | `export.template` evaluated to empty (maybe variable missing). Provide a valid literal or ensure variables exist.        |
| `Circular variable reference detected...`               | Variables reference each other recursively (e.g., `A` uses `{{B}}` and `B` uses `{{A}}`). Break the cycle.               |
| `Could not connect to MQTT/RabbitMQ server...`          | Wrong host/port or credentials. Verify connectivity, TLS requirements, firewall, and user rights.                        |
| Messages never send but process exits immediately       | `payloads` or `count` resolved to zero. Check the `count` value in the definition.                                       |
| Export files missing                                    | Global `export.enabled` false or per-payload override disabled it. Confirm configuration or disk permissions.            |

---

## Operational Tips
- **Dry runs:** Point the definition's `server`/`port` to a local broker and enable exports to inspect payloads before sending to production.
- **Parallel tuning:** Increase the `threads` value in the definition only if the target broker can handle the load; otherwise rely on sequential publishing.
- **Variable reuse:** Move shared variables to the top-level `variables` block to avoid duplicating definitions across payloads.
- **Context-aware file names:** Use `{{context.index}}` to guarantee unique outputs and to map send order.
- **Sample library:** Keep your own `definition.json` and payload templates alongside the executable (e.g., `payloads/TapEvent.json`) so deployments stay self-contained.

---

## Shutdown & Validation
1. Observe the console summary (`[payloads per iteration * count]`). Ensure it matches expectations.
2. Review broker metrics or queues to confirm messages landed.
3. Inspect export files (if enabled) to double-check content.
4. Ctrl+C stops the process if it is still publishing due to long schedules.

SendMessageToExchange streamlines the task of generating high-volume, templated messages with rich variable substitution, letting you focus on payload design rather than ad-hoc scripts. Use it for load tests, replaying traffic, or seeding integration environments.
