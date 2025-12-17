using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

using SendMessageToExchange.Definitions;
using SendMessageToExchange.Processors.Interfaces;

namespace SendMessageToExchange.Processors;

public abstract class MessageProcessorBase
{
    protected readonly SendMessageOptions _opts;
    protected readonly JsonObject _cfg;
    protected readonly string _defPath;
    protected readonly string _defaultMessageType;
    protected readonly string _defaultExchange;
    protected readonly string _defaultRoutingKey;
    protected readonly int _count;
    protected readonly int _configuredThreads;
    protected readonly int _messagesPerIteration;
    protected readonly ScheduledPayload[] _scheduledPayloads;
    protected readonly Stopwatch _executionTimeWatch;

    private readonly IPayloadBuilderProcessor _payloadProcessor;

    protected MessageProcessorBase(SendMessageOptions opts, IPayloadBuilderProcessor payloadProcessor)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _payloadProcessor = payloadProcessor ?? throw new ArgumentNullException(nameof(payloadProcessor));

        var defFullPath = Path.GetFullPath(_opts.DefinitionPath);
        _opts.DefinitionPath = defFullPath;

        _cfg = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(defFullPath))!;
        _defPath = Path.GetDirectoryName(defFullPath) ?? Directory.GetCurrentDirectory();
        _defaultMessageType = _cfg["messageType"]!.ToString();
        _defaultExchange = _cfg["exchange"]!.ToString();
        _defaultRoutingKey = _cfg["routingKey"]?.ToString() ?? _defaultMessageType;
        ApplyConnectionSettings();

        var perIterationPayloads = _payloadProcessor.Process(_cfg, _defPath);
        _messagesPerIteration = perIterationPayloads.Length;
        _count = ParseCount(_cfg.TryGetPropertyValue("count", out var countNode) ? countNode : null);
        _configuredThreads = ParseThreads(_cfg.TryGetPropertyValue("threads", out var threadsNode) ? threadsNode : null);

        _scheduledPayloads = BuildScheduledPayloads(perIterationPayloads, _count);
        LogConfigurationSummary();

        _executionTimeWatch = Stopwatch.StartNew();
    }

    public abstract Task RunAsync(CancellationToken cancellationToken);

    protected int ResolveParallelism()
    {
        var desired = _configuredThreads <= 0 ? Environment.ProcessorCount : _configuredThreads;
        desired = Math.Max(1, desired);
        return Math.Max(1, Math.Min(desired, Math.Max(1, _scheduledPayloads.Length)));
    }

    protected string BuildPayloadText(ScheduledPayload payload) => _payloadProcessor.BuildPayloadText(payload.Definition, payload.Context);

    protected ReadOnlyMemory<byte> BuildPayloadBody(ScheduledPayload payload) => _payloadProcessor.BuildPayloadBody(payload.Definition, payload.Context);

    private static int ParseCount(JsonNode? node)
    {
        if (node is null)
        {
            return 1;
        }

        if (int.TryParse(node.ToString(), out var value) && value >= 1)
        {
            return value;
        }

        return 1;
    }

    private static int ParseThreads(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (int.TryParse(node.ToString(), out var value) && value >= 0)
        {
            return value;
        }

        return 0;
    }

    private ScheduledPayload[] BuildScheduledPayloads(IReadOnlyList<PayloadDefinition> perIterationPayloads, int iterations)
    {
        if (perIterationPayloads.Count == 0 || iterations <= 0)
        {
            return Array.Empty<ScheduledPayload>();
        }

        var totalMessages = perIterationPayloads.Count * iterations;
        var scheduled = new ScheduledPayload[totalMessages];
        long counter = 1;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var offset = iteration * perIterationPayloads.Count;
            for (var i = 0; i < perIterationPayloads.Count; i++)
            {
                var definition = perIterationPayloads[i];
                var templatePath = definition.Path ?? string.Empty;
                var context = new PayloadProcessorContext(
                    counter++,
                    templatePath,
                    Path.GetFileName(templatePath) ?? string.Empty,
                    Path.GetDirectoryName(templatePath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(templatePath) ?? string.Empty);

                var exchange = string.IsNullOrWhiteSpace(definition.Exchange) ? _defaultExchange : definition.Exchange!;
                var routingKey = string.IsNullOrWhiteSpace(definition.RoutingKey) ? _defaultRoutingKey : definition.RoutingKey!;
                var messageType = string.IsNullOrWhiteSpace(definition.MessageType) ? _defaultMessageType : definition.MessageType!;

                scheduled[offset + i] = new ScheduledPayload(definition, context, exchange, routingKey, messageType);
            }
        }

        return scheduled;
    }

    protected readonly record struct ScheduledPayload(
        PayloadDefinition Definition,
        PayloadProcessorContext Context,
        string Exchange,
        string RoutingKey,
        string MessageType);

    protected Task ExecutionFinishedAsync(int totalMessages)
    {
        Console.WriteLine();
        if (_opts.Validate)
        {
            Console.WriteLine($"Validation completed successfully. [{_messagesPerIteration} payload(s) per iteration x {_count} iterations = {totalMessages} messages would be sent]");
        }
        else
        {
            Console.WriteLine($"Sent all messages. [{_messagesPerIteration} payload(s) per iteration x {_count} iterations = {totalMessages} messages]");
            Console.WriteLine(
                    $"Spent {_executionTimeWatch.Elapsed} to send messages. {Math.Round(totalMessages / _executionTimeWatch.Elapsed.TotalSeconds, 3)} messages per second");
        }

        TryPersistSequenceProgress(totalMessages);

        return Task.CompletedTask;
    }

    private void ApplyConnectionSettings()
    {
        if (TryGetNonEmptyString(_cfg, "server", out var server))
        {
            _opts.Server = server;
        }

        if (TryGetNonEmptyString(_cfg, "user", out var user))
        {
            _opts.User = user;
        }

        if (TryGetNonEmptyString(_cfg, "password", out var password))
        {
            _opts.Password = password;
        }

        if (_cfg.TryGetPropertyValue("port", out var portNode) &&
            int.TryParse(portNode?.ToString(), out var parsedPort) &&
            parsedPort > 0)
        {
            _opts.Port = parsedPort;
        }

        if (TryGetNonEmptyString(_cfg, "vhost", out var vhost))
        {
            _opts.VirtualHost = vhost;
        }

        if (string.Equals(_opts.Protocol, "mqtt", StringComparison.OrdinalIgnoreCase) &&
            TryGetNonEmptyString(_cfg, "mqttProtocolVersion", out var mqttProtocolVersion))
        {
            _opts.MqttProtocolVersion = mqttProtocolVersion;
        }
    }

    private static bool TryGetNonEmptyString(JsonObject cfg, string property, out string value)
    {
        if (cfg.TryGetPropertyValue(property, out var node))
        {
            var candidate = node?.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate!;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private void LogConfigurationSummary()
    {
        var protocolLabel = (_opts.Protocol ?? string.Empty).ToUpperInvariant();
        Console.WriteLineIfDebug($"Definition: {Path.GetFileName(_opts.DefinitionPath)} ({protocolLabel})");
        Console.WriteLineIfDebug($"Default MessageType '{_defaultMessageType}' -> Exchange '{_defaultExchange}' RoutingKey '{_defaultRoutingKey}'");
        Console.WriteLineIfDebug($"Payloads per iteration: {_messagesPerIteration}, Iterations: {_count}, Scheduled: {_scheduledPayloads.Length}");
        Console.WriteLineIfDebug($"Configured threads: {_configuredThreads}, Machine processors: {Environment.ProcessorCount}");
        var portInfo = _opts.Port?.ToString() ?? "default";
        Console.WriteLineIfDebug($"Connection target: {_opts.Server}:{portInfo} vhost '{_opts.VirtualHost}' user '{_opts.User}'");
    }

    private void TryPersistSequenceProgress(int totalMessages)
    {
        if (totalMessages <= 0)
        {
            return;
        }

        var incrementBy = totalMessages;
        var updated = false;

        void ApplyToVariables(JsonObject? variables)
        {
            if (variables is null)
            {
                return;
            }

            foreach (var kvp in variables)
            {
                if (kvp.Value is not JsonObject variableObj)
                {
                    continue;
                }

                if (!IsSequenceWithUpdate(variableObj))
                {
                    continue;
                }

                var start = TryReadInt(variableObj, "start") ?? 1;
                var step = Math.Max(1, TryReadInt(variableObj, "step") ?? 1);

                try
                {
                    var nextStart = checked((long)start + (long)incrementBy * step);
                    if (nextStart is > int.MaxValue or < int.MinValue)
                    {
                        throw new OverflowException();
                    }

                    variableObj["start"] = JsonValue.Create((int)nextStart);
                    updated = true;
                }
                catch (OverflowException)
                {
                    Console.WriteLine($"WARN: Sequence update exceeded numeric limits for a variable. Start value not changed.");
                }
            }
        }

        ApplyToVariables(_cfg.TryGetPropertyValue("variables", out var globalsNode) ? globalsNode as JsonObject : null);

        if (_cfg.TryGetPropertyValue("payloads", out var payloadsNode) && payloadsNode is JsonArray payloadsArray)
        {
            foreach (var payloadNode in payloadsArray.OfType<JsonObject>())
            {
                if (payloadNode.TryGetPropertyValue("variables", out var payloadVarsNode) && payloadVarsNode is JsonObject payloadVars)
                {
                    ApplyToVariables(payloadVars);
                }
            }
        }

        if (!updated)
        {
            return;
        }

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_opts.DefinitionPath, _cfg.ToJsonString(options));
            Console.WriteLineIfDebug($"Sequence 'start' values updated and saved to '{_opts.DefinitionPath}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Unable to persist updated sequence values: {ex.Message}");
        }
    }

    private static bool IsSequenceWithUpdate(JsonObject variableObj)
    {
        if (!variableObj.TryGetPropertyValue("type", out var typeNode))
        {
            return false;
        }

        var type = typeNode?.ToString();
        if (!string.Equals(type, "sequence", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return variableObj.TryGetPropertyValue("update", out var updateNode) && updateNode?.GetValue<bool>() == true;
    }

    private static int? TryReadInt(JsonObject obj, string property)
    {
        if (obj.TryGetPropertyValue(property, out var node) && node is not null && int.TryParse(node.ToString(), out var value))
        {
            return value;
        }

        return null;
    }
}
