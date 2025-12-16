namespace DeadLetterRescue.Definitions;

public class DeadLetterRescueOptions
{
    public string Server { get; set; } = "localhost";
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string DeadLetterQueue { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = "json";
    public string Exchange { get; set; } = string.Empty;
    public int? Port { get; set; } = null;

    public static DeadLetterRescueOptions? Parse(string[] args)
    {
        var opts = new DeadLetterRescueOptions();
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
                case "--dlq":
                    if (i + 1 < args.Length)
                    {
                        opts.DeadLetterQueue = args[++i];
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
                        opts.OutputFormat = args[++i];
                    }
                    break;
                case "--exchange":
                    if (i + 1 < args.Length)
                    {
                        opts.Exchange = args[++i];
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

        return opts;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Uso: DeadLetterRescue --dlq <deadletterqueue> [--server <host>] [--user <usuario>] [--password <clave>] [--port <puerto>]");
        Console.WriteLine("  --dlq        Queue de DeadLetter (obligatorio)");
        Console.WriteLine("  --server     Servidor RabbitMQ (por defecto: localhost)");
        Console.WriteLine("  --user       Usuario RabbitMQ (por defecto: guest)");
        Console.WriteLine("  --password   Clave RabbitMQ (por defecto: guest)");
        Console.WriteLine("  --port       Puerto de conexion (por defecto: 5672)");
    }
}
