using System.Text;
using System.Text.Json;

using DeadLetterRescue.Definitions;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DeadLetterRescue.Processors;

public class DeadLetterRescueProcessor
{
    private readonly DeadLetterRescueOptions _opts;
    public DeadLetterRescueProcessor(DeadLetterRescueOptions opts)
    {
        _opts = opts;
    }

    public async Task RunAsync()
    {
        var port = _opts.Port ?? 5672;
        var factory = new ConnectionFactory()
        {
            HostName = _opts.Server,
            UserName = _opts.User,
            Password = _opts.Password,
            VirtualHost = _opts.VirtualHost,
            Port = port
        };

        await using var conn = await factory.CreateConnectionAsync();
        var chOpts = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel = await conn.CreateChannelAsync(chOpts);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (ch, ea) =>
        {
            var body = ea.Body.ToArray();
            var routingKey = ea.RoutingKey;
            var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

            if (!string.IsNullOrEmpty(_opts.OutputFolder))
            {
                var fileName = Path.Combine(_opts.OutputFolder, $"{Ulid.NewUlid()}.{messageId}.{_opts.OutputFormat}");
                if (_opts.OutputFormat == "json")
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        MessageId = messageId,
                        RoutingKey = routingKey,
                        Body = Encoding.UTF8.GetString(body),
                        Properties = ea.BasicProperties
                    });
                    File.WriteAllText(fileName, json.Replace("\r\n", "\n"));
                }
                else
                {
                    File.WriteAllText(fileName, Encoding.UTF8.GetString(body));
                }
            }

            if (!string.IsNullOrEmpty(_opts.Exchange))
            {
                var props = new BasicProperties();
                props.MessageId = messageId;
                props.ContentType = ea.BasicProperties.ContentType;
                props.DeliveryMode = ea.BasicProperties.DeliveryMode;
                props.Type = ea.BasicProperties.Type;
                props.Headers = ea.BasicProperties.Headers;

                await channel.BasicPublishAsync(_opts.Exchange, routingKey, true, props, body);
            }

            await channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await channel.BasicConsumeAsync(_opts.DeadLetterQueue, false, consumer);
        Console.WriteLine($"Consuming from queue '{_opts.DeadLetterQueue}' on {_opts.Server}:{port}...");
        await Task.Delay(-1);
    }
}
