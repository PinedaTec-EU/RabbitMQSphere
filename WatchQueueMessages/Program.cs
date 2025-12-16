using WatchQueueMessages;

var opts = WatchQueueMessagesOptions.Parse(args);
if (opts == null)
{
    WatchQueueMessagesOptions.PrintHelp();
    return;
}

try
{
    var app = new WatchQueueMessagesApp(opts);
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
