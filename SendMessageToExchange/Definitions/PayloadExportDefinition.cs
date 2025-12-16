namespace SendMessageToExchange.Definitions;

public sealed record PayloadExportDefinition(bool Enabled, string? Template, string BasePath, bool Overwrite);
