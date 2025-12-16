using RabbitMQ.Client;

using SendMessageToExchange.Definitions;

namespace SendMessageToExchange.Processors;

public class AmqpMessageProcessor : MessageProcessorBase
{
    public AmqpMessageProcessor(SendMessageOptions opts)
        : base(opts, new PayloadBuilderProcessor())
    { }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_scheduledPayloads.Length == 0)
        {
            Console.WriteLine("No payloads scheduled.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        int port = _opts.Port ?? 5672;
        var factory = new ConnectionFactory()
        {
            HostName = _opts.Server,
            UserName = _opts.User,
            Password = _opts.Password,
            VirtualHost = _opts.VirtualHost,
            Port = port
        };

        Console.WriteLine($"Connecting to ampq://{_opts.Server}:{port} as {_opts.User} on vhost '{_opts.VirtualHost}'...");

        await using var conn = await factory.CreateConnectionAsync();
        var chOpts = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        Console.WriteLine($"Connected to {_opts.Server}:{port}");
        Console.WriteLineIfDebug($"AMQP connection ready. User '{_opts.User}', vhost '{_opts.VirtualHost}', confirmations enabled.");

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
            await using var channel = await conn.CreateChannelAsync(chOpts);
            foreach (var payload in _scheduledPayloads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await PublishWithChannelAsync(channel, payload, cancellationToken);
            }
        }
        else
        {
            Console.WriteLine($"Sending messages in parallel with {parallelism} worker(s).");
            var workers = Enumerable.Range(0, parallelism)
                .Select(workerId => RunPublishWorkerAsync(conn, chOpts, workerId, parallelism, cancellationToken));
            await Task.WhenAll(workers);
        }

        cancellationToken.ThrowIfCancellationRequested();

        await base.ExecutionFinishedAsync(totalMessages);
    }

    private async Task RunPublishWorkerAsync(IConnection conn, CreateChannelOptions options, int workerId, int parallelism, CancellationToken cancellationToken)
    {
        await using var channel = await conn.CreateChannelAsync(options);
        Console.WriteLineIfDebug($"Worker {workerId} started (stride {parallelism}).");
        for (var i = workerId; i < _scheduledPayloads.Length; i += parallelism)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PublishWithChannelAsync(channel, _scheduledPayloads[i], cancellationToken);
        }
    }

    private async Task PublishWithChannelAsync(IChannel channel, ScheduledPayload scheduled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = scheduled.Definition;
        try
        {
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Type = scheduled.MessageType,
                MessageId = Guid.NewGuid().ToString("N"),
                Headers = new Dictionary<string, object?> { ["x-message-type"] = scheduled.MessageType }
            };

            var body = BuildPayloadBody(scheduled);
            Console.WriteLineIfDebug($"Publishing '{Path.GetFileName(payload.Path)}' to '{scheduled.Exchange}'/'{scheduled.RoutingKey}' ({body.Length} bytes).");
            await channel.BasicPublishAsync(scheduled.Exchange, scheduled.RoutingKey, mandatory: true, props, body);

            Console.WriteLine($"OK -> {scheduled.Exchange} / {scheduled.RoutingKey}");
            Console.WriteLineIfDebug($"OK  {Path.GetFileName(payload.Path)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERR {Path.GetFileName(payload.Path)} -> {ex.Message}");
        }
    }
}
