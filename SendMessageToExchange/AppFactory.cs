using System.Text.Json;

using SendMessageToExchange.Definitions;
using SendMessageToExchange.Processors;

namespace SendMessageToExchange;

public class AppFactory
{
    public static MessageProcessorBase Create(SendMessageOptions opts)
    {
        if (opts is null)
        {
            throw new ArgumentNullException(nameof(opts));
        }

        if (string.IsNullOrWhiteSpace(opts.DefinitionPath))
        {
            throw new ArgumentException("Definition path is required.", nameof(opts));
        }

        var defFullPath = Path.GetFullPath(opts.DefinitionPath);
        if (!File.Exists(defFullPath))
        {
            throw new FileNotFoundException($"Definition file not found at '{defFullPath}'.");
        }

        opts.DefinitionPath = defFullPath;
        using var stream = File.OpenRead(defFullPath);
        using var doc = JsonDocument.Parse(stream);

        var protocolValue = opts.Protocol;
        if (doc.RootElement.TryGetProperty("protocol", out var protocolElement))
        {
            var candidate = protocolElement.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                protocolValue = candidate;
            }
        }

        if (protocolValue.Equals("mqtt", StringComparison.OrdinalIgnoreCase))
        {
            opts.Protocol = "mqtt";
            Console.WriteLineIfDebug("MQTT protocol selected (experimental).");

            return new MqttMessageProcessor(opts);
        }

        if (protocolValue.Equals("amqp", StringComparison.OrdinalIgnoreCase))
        {
            opts.Protocol = "amqp";
            Console.WriteLineIfDebug("AMQP protocol selected.");

            return new AmqpMessageProcessor(opts);
        }

        throw new InvalidOperationException($"Protocol '{protocolValue}' is not valid. Use 'amqp' or 'mqtt'.");
    }
}
