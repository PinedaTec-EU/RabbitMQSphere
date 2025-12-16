using System.Net.Sockets;

using MQTTnet;
using MQTTnet.Formatter;

using SendMessageToExchange.Definitions;

namespace SendMessageToExchange.Processors;

public class MqttMessageProcessor : MessageProcessorBase
{
    private bool _warnedCustomExchange;

    public MqttMessageProcessor(SendMessageOptions opts)
        : base(opts, new PayloadBuilderProcessor())
    { }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_scheduledPayloads.Length == 0)
        {
            Console.WriteLine("No payloads scheduled.");
            return;
        }

        Console.WriteLine("MQTT protocol is still experimental. Use with caution.");

        var userName = BuildMqttUsername();
        int port = _opts.Port ?? 1883;
        var protocolVersion = ResolveProtocolVersion();
        var factory = new MqttClientFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithProtocolVersion(protocolVersion)
            .WithTcpServer(_opts.Server, port)
            .WithAddressFamily(AddressFamily.Unspecified)
            .WithTlsOptions(o => o.UseTls(false))
            .WithCredentials(userName, _opts.Password)
            .Build();
        Console.WriteLineIfDebug($"MQTT target {_opts.Server}:{port} | default topic '{_defaultExchange}' | user '{userName}' | protocol {protocolVersion}.");

        Console.WriteLine($"Connecting to mqtt://{_opts.Server}:{port} as {_opts.User} on vhost '{_opts.VirtualHost}'...");

        await mqttClient.ConnectAsync(options, CancellationToken.None);
        try
        {
            if (!mqttClient.IsConnected)
            {
                Console.WriteLine($"Could not connect to MQTT server {_opts.Server}:{port}");
                return;
            }
            Console.WriteLine($"Connected to MQTT {_opts.Server}:{port}");

            var totalMessages = _scheduledPayloads.Length;
            if (totalMessages == 0)
            {
                Console.WriteLine("No messages scheduled to send.");
                return;
            }

            var parallelism = ResolveParallelism();
            if (parallelism <= 1)
            {
                Console.WriteLineIfDebug("Publishing sequentially with a single worker.");
                foreach (var payload in _scheduledPayloads)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await PublishMqttAsync(mqttClient, payload, cancellationToken);
                }
            }
            else
            {
                Console.WriteLine($"Sending messages in parallel with {parallelism} worker(s).");
                var workers = Enumerable.Range(0, parallelism)
                    .Select(workerId => RunMqttWorkerAsync(mqttClient, workerId, parallelism, cancellationToken));
                await Task.WhenAll(workers);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await base.ExecutionFinishedAsync(totalMessages);
        }
        finally
        {
            if (mqttClient.IsConnected)
            {
                try
                {
                    var disconnectOptions = new MqttClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build();
                    await mqttClient.DisconnectAsync(disconnectOptions, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Ignore disconnect errors during shutdown.
                }
            }
        }
    }

    private async Task RunMqttWorkerAsync(IMqttClient client, int workerId, int parallelism, CancellationToken cancellationToken)
    {
        Console.WriteLineIfDebug($"Worker {workerId} started (stride {parallelism}).");
        for (var i = workerId; i < _scheduledPayloads.Length; i += parallelism)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PublishMqttAsync(client, _scheduledPayloads[i], cancellationToken);
        }
    }

    private async Task PublishMqttAsync(IMqttClient client, ScheduledPayload scheduled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = scheduled.Definition;
        try
        {
            var topic = string.IsNullOrWhiteSpace(scheduled.RoutingKey)
                ? scheduled.Exchange
                : scheduled.RoutingKey;
            WarnIfCustomExchange(scheduled.Exchange);
            var body = BuildPayloadText(scheduled);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(body)
                .Build();
            Console.WriteLineIfDebug($"Publishing '{Path.GetFileName(payload.Path)}' to MQTT topic '{topic}' (payload length {body.Length} chars).");

            var result = await client.PublishAsync(message, cancellationToken);

            if (result.IsSuccess)
            {
                Console.WriteLine($"OK -> MQTT {topic}");
                Console.WriteLineIfDebug($"OK  {Path.GetFileName(payload.Path)}");
            }
            else
            {
                Console.WriteLine($"Error: {result.ReasonString}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERR {Path.GetFileName(payload.Path)} -> {ex.Message}");
        }
    }

    private void WarnIfCustomExchange(string exchange)
    {
        if (_warnedCustomExchange)
        {
            return;
        }

        if (!string.Equals(exchange, "amq.topic", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"WARN: MQTT ignores exchange '{exchange}'. routingKey is always used as the topic.");
            _warnedCustomExchange = true;
        }
    }

    private MqttProtocolVersion ResolveProtocolVersion()
    {
        var versionText = _opts.MqttProtocolVersion?.Trim();
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return MqttProtocolVersion.V311;
        }

        return versionText.ToLowerInvariant() switch
        {
            "v310" => MqttProtocolVersion.V310,
            "v311" => MqttProtocolVersion.V311,
            "v500" => MqttProtocolVersion.V500,
            _ => throw new InvalidOperationException($"La version MQTT '{versionText}' no es valida. Usa 'v310', 'v311' o 'v500'.")
        };
    }

    private string BuildMqttUsername()
    {
        var username = _opts.User;
        var vhost = _opts.VirtualHost?.Trim();
        if (string.IsNullOrWhiteSpace(vhost) || vhost == "/")
        {
            return username;
        }

        if (username.Contains(':'))
        {
            return username;
        }

        return $"{vhost}:{username}";
    }
}
