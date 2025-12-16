using DeadLetterRescue.Definitions;
using DeadLetterRescue.Processors;

var opts = DeadLetterRescueOptions.Parse(args);
if (opts == null)
{
    DeadLetterRescueOptions.PrintHelp();
    return;
}

try
{
    var app = new DeadLetterRescueProcessor(opts);
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
