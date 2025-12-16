# SendMessageToExchange User Guide

SendMessageToExchange is a flexible publisher harness for RabbitMQ/MQTT that loads message templates, applies variables (including random generators and per-message context), and sends batches to a target exchange/topic. It also supports exporting rendered payloads to disk for auditing.

---

## Prerequisites
- .NET 10.0 SDK or newer.
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

All connectivity settings (protocol, server, port, credentials, vhost) live inside the definition file and fall back to the same defaults the CLI exposed previously.

---

## Definition File Anatomy

Minimal example definition (`definition.def`):
```json
{
  "protocol": "amqp",
  "server": "rabbitmq.dev.local",
  "port": 5672,
  "user": "publisher",
  "password": "Pa55!",
  "vhost": "/",
  "messageType": "GMV.ITS.Suite.QueueMessages.Tap.OfflineTap",
  "exchange": "amq.topic",
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

Connection properties (definition-level):

| Property   | Required | Default                       | Description                               |
| ---------- | -------- | ----------------------------- | ----------------------------------------- |
| `protocol` | No       | `amqp`                        | `amqp` (RabbitMQ) or `mqtt`.              |
| `server`   | No       | `localhost`                   | Broker host/IP.                           |
| `port`     | No       | `5672` (AMQP) / `1883` (MQTT) | Overrides the default protocol port.      |
| `vhost`    | No       | `/`                           | RabbitMQ virtual host (ignored for MQTT). |
| `user`     | No       | `guest`                       | Username for AMQP/MQTT authentication.    |
| `password` | No       | `guest`                       | Password for AMQP/MQTT authentication.    |

For MQTT connections the tool automatically prefixes the username with `"<vhost>:<user>"` when the virtual host is different from `/` and the configured user does not already include a colon. This matches the credential pattern expected by the RabbitMQ MQTT plugin (`mqtt://<vhost>:<user>:<password>@host`). If you prefer to manage the prefix yourself, define the username exactly as the broker expects (e.g., `"user": "suite:test"`), and it will be used as-is.

> ⚠️ ITS Suite note: `messageType` and `routingKey` are typically identical. If you set both and they differ, the effective routing uses `routingKey` (global and per-payload). If they are the same, you only need to specify one.

Key sections:
- `variables`: global tokens available to every payload. Supports literals or random generators (`number`, `text`, `guid`, `ulid`, `datetime`, `date`, `time`).
- `payloads`: list of template entries.
  - Tokens like `{{TapId}}` are resolved whenever matching variables/context exist; missing variables throw an error.
  - `count` duplicates the payload within each iteration.
  - Each payload can override/extend `variables`, `export`, and routing (`exchange`, `routingKey`, `messageType`).
- `formatting`: optional object with `date`, `time`, and `datetime` entries that override the default `yyyy-MM-dd` / `HH:mm:ss` / `O` formats used by the random generators. Setting a `format` directly on a variable definition takes precedence over these global values.
- `export`: optional. When enabled, each rendered payload is written to disk using your template (no automatic suffixing). Example: `Exports/{{GlobalCorrelationId}}/{{context.templateFileNameStem}}-{{context.index:D4}}.json`. The export template supports the same variables and context tokens as payload bodies. You can also set `overwrite: true` to allow replacing existing files.
- Context tokens (always accessible): `{{context.index}}` resolves to the sequential message counter.

### Formatting precedence
1. **Per-variable format**: Add `"format": "<custom>"` inside any `datetime`, `date` or `time` variable definition to force a specific output (for example, `"format": "yyyyMMddHHmmss"`). These are the types where custom output formats apply.
2. **Global formatting block**: Use the top-level `formatting` object to define defaults for every variable of that type (`date`, `time`, `datetime`). These defaults apply whenever an individual variable omits its own `format`.
3. **Built-in defaults**: If neither of the above is specified, the generator falls back to `yyyy-MM-dd`, `HH:mm:ss`, and the ISO 8601 `O` format respectively.

You can still apply ad-hoc formatting while templating (e.g., `{{EventDate:yyyy/MM/dd}}`), but those expressions work on top of the already-formatted string emitted by the generator.

### Variable Types & Defaults
Variables accept either a literal object (`{ "value": "foo" }` or `"foo"`) or a random generator with a `type`. Available types:

| Type       | Purpose                                        |
| ---------- | ---------------------------------------------- |
| `number`   | Integer randomizer.                            |
| `sequence` | Deterministic counter tied to `context.index`. |
| `text`     | Random alphanumeric string.                    |
| `guid`     | Generates `Guid.NewGuid()`.                    |
| `ulid`     | Generates `Ulid.NewUlid()`.                    |
| `datetime` | Random `DateTimeOffset`.                       |
| `date`     | Random `DateOnly`.                             |
| `time`     | Random `TimeOnly`.                             |
| `fixed`    | Explicit literal value using object syntax.    |

When both a global `formatting` entry and a per-variable `format` are provided, the per-variable value wins. You can still apply ad-hoc formatting in templates using the `{{Variable:format}}` syntax, which operates on the already-formatted string.

#### `number`
- Optional: `min`, `max` (inclusive), `padding` (digits).
- Defaults: `min=1`, `max=100`.
- Notes: Padding uses `D<padding>` (e.g., padding 4 turns `7` into `0007`).
```json
"OrderId": { "type": "number", "min": 10, "max": 9999, "padding": 5 }
```

#### `sequence`
- Optional: `start` (default 1), `step` (default 1), `padding`.
- Defaults: emits `start + (context.index - 1) * step`, e.g., `100`, `101`, `102`.
- Notes: Padding works like the `number` type to emit `00100`, `00101`, etc.
```json
"BatchSequence": { "type": "sequence", "start": 500, "step": 10, "padding": 4 }
```

#### `text`
- Optional: `length`.
- Defaults: length `16`.
- Notes: Enforces minimum length 1. Example output: `q4f9p1mx7bn2s3ty`.
```json
"SessionToken": { "type": "text", "length": 24 }
```

#### `guid`
- Optional: none.
- Defaults: lowercase GUID string.
- Notes: Emits `Guid.NewGuid()`, e.g., `3f1c2c0f3a5e4c9ab7d8c6f1a2b3c4d5`.
```json
"GlobalCorrelationId": { "type": "guid" }
```

#### `ulid`
- Optional: none.
- Defaults: lexicographically sortable ULID.
- Notes: Emits `Ulid.NewUlid()`, e.g., `01JABCD2EFGHJKL3MNOPQRSTUV`.
```json
"TapId": { "type": "ulid" }
```

#### `datetime`
- Optional: `from`, `to`, `format`.
- Defaults: `UtcNow - 1 month` to `UtcNow + 1 month`.
- Notes: ISO 8601 output such as `2025-10-22T09:46:46Z`. `format` overrides `formatting.datetime`.
```json
"EventTimestamp": {
  "type": "datetime",
  "from": "2025-01-01T00:00:00Z",
  "to": "2025-12-31T23:59:59Z",
  "format": "yyyy-MM-ddTHH:mm:ssZ"
}
```

#### `date`
- Optional: `from`, `to`, `format`.
- Defaults: +/-1 month window around today.
- Notes: Example output: `2025-10-22`. `format` overrides `formatting.date`.
```json
"EventDate": {
  "type": "date",
  "from": "2025-01-01",
  "to": "2025-12-31",
  "format": "yyyy/MM/dd"
}
```

#### `time`
- Optional: `from`, `to`, `format`.
- Defaults: `00:00:00` to `23:59:59.9999999`.
- Notes: Example output: `14:05:33`. `format` overrides `formatting.time`.
```json
"EventTime": {
  "type": "time",
  "from": "08:00:00",
  "to": "18:00:00",
  "format": "HH:mm:ss"
}
```

#### `fixed`
- Required: `value`.
- Defaults: literal value you supply.
- Notes: Equivalent to writing a plain string; useful for schema consistency (e.g., `SampleTenant`). Fails if `value` is missing or empty.
```json
"Tenant": { "type": "fixed", "value": "GMV-ITS" }
```

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

### Payload configuration (`payloads` array)
Each entry inside `payloads` controls how a template is rendered, duplicated, and routed.

- String entry: `"payloads": ["payloads/TapEvent.json"]` — shorthand for an object with `count: 1` and only global variables/exports applied.
- Object entry: full control over duplication, variables, and routing overrides.

Payload object properties:

| Property      | Required | Default                            | Description                                                                                                             |
| ------------- | -------- | ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `path`        | Yes      | –                                  | Path to the template file. Resolved relative to the definition file.                                                    |
| `count`       | No       | `1`                                | How many times to repeat this template per iteration. Combined with the root `count` iterations.                        |
| `variables`   | No       | inherits global `variables`        | Per-payload variables merged with globals. Case-insensitive; payload values override global ones.                       |
| `exchange`    | No       | root `exchange`                    | Override the exchange/topic for this payload only.                                                                      |
| `routingKey`  | No       | root `routingKey` or `messageType` | Override routing key/topic for this payload only (MQTT uses `exchange`/`routingKey` as the topic).                      |
| `messageType` | No       | root `messageType`                 | Override the message type header for this payload only.                                                                 |
| `export`      | No       | inherits root `export`             | Enable/disable or change export template per payload. `export.enabled: false` disables export even if enabled globally. |

Behavior and rules:
- **Variable resolution:** When variables (global or per-payload) exist, every `{{Token}}` must have a definition or a `context.*` counterpart. Missing tokens throw an error. Random values are generated once per message and reused within that render.
- **Formatting:** Inline `{{Var:D4}}` applies .NET numeric formats. Date/time respect precedence: variable `format` > root `formatting` > built-in defaults. Inline `:format` still applies on top of the generated string.
- **Context tokens:** Always available (no variable definition needed) for uniqueness and naming (`{{context.index}}`, `{{context.templateFileNameStem}}`, etc.).
- **Duplication math:** Messages sent for one payload entry = `payload.count * root.count`. Example: `payload.count = 3` and root `count = 2` yields 6 sends of that template.
- **Export inheritance:** If the root `export.enabled` is true, every payload exports unless its own `export.enabled` is false. Payload-level `export.enabled: true` requires its own `template`. `overwrite: true` allows replacing existing files.
- **Per-payload routing:** Use per-payload `exchange`/`routingKey`/`messageType` to target different exchanges/topics within the same run without duplicating definition files.

#### Example: two payloads with different targets and export override
```json
{
  "messageType": "GMV.ITS.Suite.QueueMessages.Tap.OfflineTap",
  "exchange": "amq.topic",
  "routingKey": "GMV.ITS.Suite.QueueMessages.Tap.OfflineTap",
  "count": 2,
  "variables": {
    "GlobalCorrelationId": { "type": "guid" },
    "Tenant": "GMV-ITS"
  },
  "export": {
    "enabled": true,
    "template": "Exports/{{GlobalCorrelationId}}/{{context.templateFileNameStem}}-{{context.index:D4}}.json"
  },
  "payloads": [
    {
      "path": "payloads/TapEvent.json",
      "count": 3,
      "variables": {
        "TapId": { "type": "ulid" },
        "TapSequence": { "type": "sequence", "start": 100, "padding": 4 }
      }
    },
    {
      "path": "payloads/AlertEvent.json",
      "count": 1,
      "exchange": "alerts.topic",
      "routingKey": "Alerts.OfflineTap",
      "messageType": "GMV.ITS.Suite.QueueMessages.Alert",
      "variables": {
        "AlertId": { "type": "guid" },
        "Severity": { "value": "high" }
      },
      "export": {
        "enabled": true,
        "template": "Exports/Alerts/{{context.index:D4}}.json",
        "overwrite": true
      }
    }
  ]
}
```
Results:
- Two iterations. First payload: 3 sends per iteration → 6 messages to `amq.topic` using the root routing key.
- Second payload: 1 send per iteration → 2 messages to `alerts.topic`/`Alerts.OfflineTap` with its own `messageType`.
- Root export applies to the first payload. The second uses its own pattern and allows overwrite.

### Context Tokens
During rendering each payload receives a `PayloadProcessorContext`. The following properties are exposed via `{{context.*}}`:

| Token                                                                               | Description                                                                                                                                                                                                                            |
| ----------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `{{context.index}}`                                                                 | 1-based running counter across all scheduled messages (respecting repetitions and iterations). Use standard formatting (e.g., `{{context.index}}` inside `string.Format` style or suffix logic) to produce padded values such as `D4`. |
| `{{context.templateFilePath}}`                                                      | Absolute path of the template file currently being processed. Useful when export paths need to mirror the source structure.                                                                                                            |
| `{{context.templateFileName}}`                                                      | File name (with extension) derived from the template path, e.g., `TapEvent.json`.                                                                                                                                                      |
| `{{context.templateDirectory}}`                                                     | Directory component of the template path.                                                                                                                                                                                              |
| `{{context.templateFileNameWithoutExtension}}` / `{{context.templateFileNameStem}}` | File name without the extension, e.g., `TapEvent`.                                                                                                                                                                                     |

These context tokens are available everywhere templates are resolved (payload bodies and export path templates), so you can build file names such as `Exports/{{context.templateFileNameStem}}/{{context.index:D4}}.json`. Any identifier without the `context.` prefix is treated as a variable name. Every token accepts an optional `:format` suffix, so `{{InvoiceNumber:D6}}` or `{{context.index:D4}}` apply .NET-style numeric formatting without modifying the variable definition.

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
3. `MessageProcessorBase` enqueues scheduled deliveries (`count` × payload entries).
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
- Console output shows “OK <template name>” per message and final statistics.

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
