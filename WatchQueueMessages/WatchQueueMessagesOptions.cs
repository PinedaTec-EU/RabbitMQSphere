namespace WatchQueueMessages;

public class WatchQueueMessagesOptions
{
    public string Server { get; set; } = "localhost";
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Queue { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string FileFormat { get; set; } = "json";
    public int? Port { get; set; } = null;

    public static WatchQueueMessagesOptions? Parse(string[] args)
    {
        var opts = new WatchQueueMessagesOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--server":
                    if (i + 1 < args.Length)
                    {
                        opts.Server = args[++i];
                    }
                    break;
                case "--vhost":
                    if (i + 1 < args.Length)
                    {
                        opts.VirtualHost = args[++i];
                    }
                    break;
                case "--user":
                    if (i + 1 < args.Length)
                    {
                        opts.User = args[++i];
                    }
                    break;
                case "--password":
                    if (i + 1 < args.Length)
                    {
                        opts.Password = args[++i];
                    }
                    break;
                case "--queue":
                    if (i + 1 < args.Length)
                    {
                        opts.Queue = args[++i];
                    }
                    break;
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        opts.OutputFolder = args[++i];
                    }
                    break;
                case "--format":
                    if (i + 1 < args.Length)
                    {
                        opts.FileFormat = args[++i];
                    }
                    break;
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int port))
                    {
                        opts.Port = port;
                        i++;
                    }
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(opts.Queue) || string.IsNullOrWhiteSpace(opts.OutputFolder))
        {
            return null;
        }

        return opts;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Uso: WatchQueueMessages --queue <nombre_queue> --output <carpeta> [--server <host>] [--user <usuario>] [--password <clave>] [--vhost <vhost>] [--format <json|txt>] [--port <puerto>]");
        Console.WriteLine("  --queue      Nombre de la queue a consumir (obligatorio)");
        Console.WriteLine("  --output     Carpeta donde dejar los mensajes (obligatorio)");
        Console.WriteLine("  --server     Servidor RabbitMQ (por defecto: localhost)");
        Console.WriteLine("  --user       Usuario RabbitMQ (por defecto: guest)");
        Console.WriteLine("  --password   Clave RabbitMQ (por defecto: guest)");
        Console.WriteLine("  --vhost      Virtual host (por defecto: /)");
        Console.WriteLine("  --port       Puerto de conexion (por defecto: 5672)");
    }
}
