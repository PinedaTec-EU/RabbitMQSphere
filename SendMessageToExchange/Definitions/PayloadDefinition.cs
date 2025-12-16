namespace SendMessageToExchange.Definitions;

public sealed record PayloadDefinition(
    string Path,
    string Template,
    int Count,
    IReadOnlyDictionary<string, VariableDefinition>? Variables,
    PayloadExportDefinition? Export,
    string? Exchange = null,
    string? RoutingKey = null,
    string? MessageType = null);
