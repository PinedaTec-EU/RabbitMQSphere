using SendMessageToExchange;
using SendMessageToExchange.Definitions;

var opts = SendMessageOptions.Parse(args);
if (opts == null)
{
    SendMessageOptions.PrintHelp();
    return;
}

Console.ConfigureDebug(opts.Debug);

using var cts = new CancellationTokenSource();
ConsoleCancelEventHandler? handler = null;
handler = (_, eventArgs) =>
{
    if (!cts.IsCancellationRequested)
    {
        Console.WriteLine("Cancellation requested. Press Ctrl+C again to force exit.");
    }

    eventArgs.Cancel = true;
    cts.Cancel();
};
Console.CancelKeyPress += handler;

try
{
    Console.WriteLine("SendMessageToExchange starting...");
    var app = AppFactory.Create(opts);
    await app.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled by user.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
finally
{
    if (handler is not null)
    {
        Console.CancelKeyPress -= handler;
    }
}
