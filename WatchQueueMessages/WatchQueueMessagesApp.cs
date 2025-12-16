using System.Text;
using System.Text.Json;

using RabbitMQ.Client;

namespace WatchQueueMessages;

public class WatchQueueMessagesApp
{
    private readonly WatchQueueMessagesOptions _opts;

    public WatchQueueMessagesApp(WatchQueueMessagesOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
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

        Console.WriteLine($"Connected to {_opts.Server}:{port}");

        Directory.CreateDirectory(_opts.OutputFolder);

        var consumer = new RabbitMQ.Client.Events.AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();
                var ulid = Ulid.NewUlid().ToString().ToLower();
                var propsFileName = Path.Combine(_opts.OutputFolder, $"{ulid}.props.json");
                var fileName = Path.Combine(_opts.OutputFolder, $"{ulid}.{_opts.FileFormat}");

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var properties = JsonSerializer.Serialize(new
                {
                    MessageId = messageId,
                    Properties = ea.BasicProperties
                }, options);

                File.WriteAllText(propsFileName, properties);

                if (_opts.FileFormat == "json")
                {
                    // Guardar con saltos de línea \n
                    File.WriteAllText(fileName, Encoding.UTF8.GetString(body));
                }
                else
                {
                    File.WriteAllText(fileName, Encoding.UTF8.GetString(body));
                }

                Console.WriteLine($"Mensaje guardado: {fileName}");

                // Acknowledge the message
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error al procesar el mensaje: {ex.Message}");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);

                return;
            }
        };

        await channel.BasicConsumeAsync(queue: _opts.Queue, autoAck: false, consumer: consumer);

        Console.WriteLine($"Escuchando mensajes en la queue '{_opts.Queue}' en {_opts.Server}:{port}...");
        Console.WriteLine("Presiona Ctrl+C para salir.");

        while (true)
        {
            Thread.Sleep(1000);
        }
    }
}
