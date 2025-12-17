namespace SendMessageToExchange.Definitions;

public class SendMessageOptions
{
    public string DefinitionPath { get; set; } = string.Empty;
    public string Protocol { get; set; } = "amqp";
    public string Server { get; set; } = "localhost";
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public int? Port { get; set; } = null;
    public bool Debug { get; set; }
    public bool Validate { get; set; }
    public string? MqttProtocolVersion { get; set; }

    public static SendMessageOptions? Parse(string[] args)
    {
        var opts = new SendMessageOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--def":
                    if (i + 1 < args.Length)
                    {
                        opts.DefinitionPath = args[++i];
                    }
                    break;
                case "--debug":
                    opts.Debug = true;
                    break;
                case "--validate":
                    opts.Validate = true;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(opts.DefinitionPath) || !File.Exists(opts.DefinitionPath))
        {
            return null;
        }

        return opts;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Usage: SendMessageToExchange --def <path_to_json> [options]");
        Console.WriteLine("  --def        Path to the definition JSON file (required)");
        Console.WriteLine("  --debug      Enable verbose console messages (optional)");
        Console.WriteLine("  --validate   Validate the definition file without sending messages (optional)");
        Console.WriteLine();
        Console.WriteLine("Server, credentials, protocol, and port are defined inside the definition file.");
    }
}
