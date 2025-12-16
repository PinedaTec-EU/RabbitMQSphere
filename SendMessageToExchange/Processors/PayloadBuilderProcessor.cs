using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using SendMessageToExchange.Definitions;
using SendMessageToExchange.Processors.Interfaces;
using SendMessageToExchange.Services;
using SendMessageToExchange.Services.Interfaces;

namespace SendMessageToExchange.Processors;

public sealed class PayloadBuilderProcessor : IPayloadBuilderProcessor
{
    private static readonly Regex VariableTokenRegex = new(@"\{\{([A-Za-z0-9_\-\.]+)(?::([^}]+))?\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const int DefaultTextLength = 16;
    private readonly IPayloadExportService _exportService;
    private readonly IRandomValueService _valueService;
    private RandomValueFormattingOptions _formattingOptions = RandomValueFormattingOptions.Default;

    public PayloadBuilderProcessor()
        : this(new PayloadExportService(), new DynamicValueService())
    {
    }

    public PayloadBuilderProcessor(IPayloadExportService exportService, IRandomValueService valueService)
    {
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _valueService = valueService ?? throw new ArgumentNullException(nameof(valueService));
    }

    public PayloadDefinition[] Process(JsonObject config, string basePath)
    {
        if (config["payloads"] is not JsonArray payloadArray)
        {
            throw new InvalidOperationException("The configuration must include a 'payloads' array.");
        }

        var globalVariables = ParseVariableDictionary(config.TryGetPropertyValue("variables", out var globalsNode) ? globalsNode : null);
        _formattingOptions = ParseFormattingOptions(config.TryGetPropertyValue("formatting", out var formattingNode) ? formattingNode : null);
        var exportDefaults = ParseExportDefinition(config.TryGetPropertyValue("export", out var exportNode) ? exportNode : null, basePath, null);
        var definitions = payloadArray.Select(node => ParsePayloadDefinition(node!, basePath, globalVariables, exportDefaults)).ToArray();

        return definitions.SelectMany(def => Enumerable.Repeat(def, def.Count)).ToArray();
    }

    public string BuildPayloadText(PayloadDefinition payload, PayloadProcessorContext context)
    {
        VariableResolver? resolver = null;
        if (payload.Variables is { Count: > 0 })
        {
            resolver = new VariableResolver(payload.Variables, _valueService, _formattingOptions, context);
        }

        var content = ResolveTemplate(payload.Template, payload, context, resolver);

        _exportService.Export(payload, context, content, template => ResolveTemplate(template, payload, context, resolver));
        return content;
    }

    public ReadOnlyMemory<byte> BuildPayloadBody(PayloadDefinition payload, PayloadProcessorContext context)
    {
        var content = BuildPayloadText(payload, context);
        return Encoding.UTF8.GetBytes(content);
    }

    private static PayloadDefinition ParsePayloadDefinition(
        JsonNode node,
        string basePath,
        IReadOnlyDictionary<string, VariableDefinition>? globalVariables,
        PayloadExportDefinition? exportDefaults)
    {
        if (node is JsonValue valueNode)
        {
            var relativePath = valueNode.GetValue<string>();
            var absolutePath = Path.GetFullPath(relativePath, basePath);
            var template = File.ReadAllText(absolutePath);

            var combinedVariables = BuildVariableDictionary(globalVariables, null);

            return new PayloadDefinition(
                absolutePath,
                template,
                Count: 1,
                Variables: combinedVariables,
                Export: exportDefaults);
        }

        if (node is JsonObject obj)
        {
            if (!obj.TryGetPropertyValue("path", out var pathNode) || pathNode is null)
            {
                throw new InvalidOperationException("Each payload entry must include a 'path'.");
            }

            var absolutePath = Path.GetFullPath(pathNode.GetValue<string>(), basePath);
            var count = ParseCount(obj.TryGetPropertyValue("count", out var countNode) ? countNode : null);
            var payloadVariables = ParseVariableDictionary(obj.TryGetPropertyValue("variables", out var payloadVarsNode) ? payloadVarsNode : null);
            var combinedVariables = BuildVariableDictionary(globalVariables, payloadVariables);
            var payloadExportNode = obj.TryGetPropertyValue("export", out var perPayloadExportNode) ? perPayloadExportNode : null;
            var payloadExport = ParseExportDefinition(payloadExportNode, basePath, exportDefaults);
            var template = File.ReadAllText(absolutePath);
            var payloadExchange = GetOptionalString(obj, "exchange");
            var payloadRoutingKey = GetOptionalString(obj, "routingKey");
            var payloadMessageType = GetOptionalString(obj, "messageType");

            return new PayloadDefinition(
                absolutePath,
                template,
                count,
                combinedVariables,
                payloadExport,
                Exchange: payloadExchange,
                RoutingKey: payloadRoutingKey,
                MessageType: payloadMessageType);
        }

        throw new InvalidOperationException("Unsupported payload entry. Use either a string path or an object with 'path'.");
    }

    private static IReadOnlyDictionary<string, VariableDefinition>? BuildVariableDictionary(
        IReadOnlyDictionary<string, VariableDefinition>? globals,
        IReadOnlyDictionary<string, VariableDefinition>? payload)
    {
        var hasGlobal = globals is { Count: > 0 };
        var hasPayload = payload is { Count: > 0 };

        if (!hasGlobal && !hasPayload)
        {
            return null;
        }

        if (hasGlobal && !hasPayload)
        {
            return globals;
        }

        if (!hasGlobal)
        {
            return payload;
        }

        var dict = new Dictionary<string, VariableDefinition>(globals!, StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in payload!)
        {
            dict[kvp.Key] = kvp.Value;
        }

        return dict;
    }

    private static IReadOnlyDictionary<string, VariableDefinition>? ParseVariableDictionary(JsonNode? node)
    {
        if (node is not JsonObject obj || obj.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, VariableDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in obj)
        {
            if (kvp.Value is null)
            {
                continue;
            }

            dict[kvp.Key] = ParseVariableDefinition(kvp.Value);
        }

        return dict;
    }

    private static VariableDefinition ParseVariableDefinition(JsonNode node)
    {
        if (node is JsonValue literal)
        {
            return new VariableDefinition(literal.ToString(), null);
        }

        if (node is JsonObject obj)
        {
            if (!obj.TryGetPropertyValue("type", out var typeNode) || typeNode is null)
            {
                if (obj.TryGetPropertyValue("value", out var literalNode) && literalNode is not null)
                {
                    return new VariableDefinition(literalNode.ToString(), null);
                }

                throw new InvalidOperationException("Variable object definitions must include a 'type'.");
            }

            var type = typeNode.GetValue<string>()?.Trim().ToLowerInvariant()
                ?? throw new InvalidOperationException("Variable 'type' must be a string.");

            var definition = type switch
            {
                "number" => ParseRandomNumberDefinition(obj),
                "text" => ParseRandomTextDefinition(obj),
                "guid" => new RandomValueDefinition(RandomValueType.Guid),
                "ulid" => new RandomValueDefinition(RandomValueType.Ulid),
                "datetime" => ParseRandomDateTimeDefinition(obj),
                "date" => ParseRandomDateDefinition(obj),
                "time" => ParseRandomTimeDefinition(obj),
                "sequence" => ParseSequenceDefinition(obj),
                "fixed" => null,
                _ => throw new InvalidOperationException($"Unsupported random generator type '{type}'.")
            };

            if (type == "fixed")
            {
                if (!obj.TryGetPropertyValue("value", out var fixedValueNode) || fixedValueNode is null)
                {
                    throw new InvalidOperationException("Fixed variables must include a 'value'.");
                }

                var fixedValue = fixedValueNode.ToString();
                if (string.IsNullOrWhiteSpace(fixedValue))
                {
                    throw new InvalidOperationException("Fixed variable 'value' cannot be empty.");
                }

                return new VariableDefinition(fixedValue, null);
            }

            return new VariableDefinition(null, definition);
        }

        throw new InvalidOperationException("Unsupported variable definition. Use literals or objects with a 'type'.");
    }

    private static RandomValueDefinition ParseRandomNumberDefinition(JsonObject obj)
    {
        var min = TryParseInt(obj, "min") ?? 1;
        var max = TryParseInt(obj, "max") ?? 100;
        var padding = TryParseInt(obj, "padding");
        if (padding.HasValue)
        {
            padding = Math.Max(0, padding.Value);
        }
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return new RandomValueDefinition(RandomValueType.Number, Min: min, Max: max, Padding: padding);
    }

    private static RandomValueDefinition ParseRandomTextDefinition(JsonObject obj)
    {
        var length = TryParseInt(obj, "length") ?? DefaultTextLength;
        length = Math.Max(1, length);
        return new RandomValueDefinition(RandomValueType.Text, Length: length);
    }

    private static RandomValueDefinition ParseRandomDateTimeDefinition(JsonObject obj)
    {
        var from = TryParseDateTime(obj, "from") ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var to = TryParseDateTime(obj, "to") ?? DateTimeOffset.UtcNow.AddMonths(1);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var format = TryParseFormatString(obj);
        return new RandomValueDefinition(RandomValueType.DateTime, FromDateTime: from, ToDateTime: to, Format: format);
    }

    private static RandomValueDefinition ParseRandomDateDefinition(JsonObject obj)
    {
        var defaultFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var defaultTo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
        var from = TryParseDateOnly(obj, "from") ?? defaultFrom;
        var to = TryParseDateOnly(obj, "to") ?? defaultTo;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var format = TryParseFormatString(obj);
        return new RandomValueDefinition(RandomValueType.Date, FromDate: from, ToDate: to, Format: format);
    }

    private static RandomValueDefinition ParseRandomTimeDefinition(JsonObject obj)
    {
        var from = TryParseTimeOnly(obj, "from") ?? TimeOnly.MinValue;
        var to = TryParseTimeOnly(obj, "to") ?? TimeOnly.MaxValue;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var format = TryParseFormatString(obj);
        return new RandomValueDefinition(RandomValueType.Time, FromTime: from, ToTime: to, Format: format);
    }

    private static RandomValueDefinition ParseSequenceDefinition(JsonObject obj)
    {
        var start = TryParseInt(obj, "start") ?? 1;
        var step = TryParseInt(obj, "step") ?? 1;
        step = Math.Max(1, step);
        var update = obj.TryGetPropertyValue("update", out var updateNode) && updateNode?.GetValue<bool>() == true;
        var padding = TryParseInt(obj, "padding");
        if (padding.HasValue)
        {
            padding = Math.Max(0, padding.Value);
        }
        return new RandomValueDefinition(RandomValueType.Sequence, Start: start, Step: step, Padding: padding, Update: update);
    }

    private static RandomValueFormattingOptions ParseFormattingOptions(JsonNode? node)
    {
        var defaults = RandomValueFormattingOptions.Default;
        if (node is not JsonObject obj || obj.Count == 0)
        {
            return defaults;
        }

        var dateFormat = obj.TryGetPropertyValue("date", out var dateNode) ? dateNode?.ToString() : null;
        var timeFormat = obj.TryGetPropertyValue("time", out var timeNode) ? timeNode?.ToString() : null;

        var dateTimeFormat = obj.TryGetPropertyValue("datetime", out var dateTimeNode) ? dateTimeNode?.ToString() : null;

        var resolvedDate = string.IsNullOrWhiteSpace(dateFormat) ? defaults.DateFormat : dateFormat!;
        var resolvedTime = string.IsNullOrWhiteSpace(timeFormat) ? defaults.TimeFormat : timeFormat!;
        var resolvedDateTime = string.IsNullOrWhiteSpace(dateTimeFormat) ? defaults.DateTimeFormat : dateTimeFormat!;

        return new RandomValueFormattingOptions(resolvedDate, resolvedTime, resolvedDateTime);
    }

    private static PayloadExportDefinition? ParseExportDefinition(JsonNode? node, string basePath, PayloadExportDefinition? fallback)
    {
        if (node is null)
        {
            return fallback;
        }

        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("Export definitions must be objects.");
        }

        var enabled = obj.TryGetPropertyValue("enabled", out var enabledNode) && enabledNode?.GetValue<bool>() == true;
        if (!enabled)
        {
            return new PayloadExportDefinition(false, null, basePath, false);
        }

        if (!obj.TryGetPropertyValue("template", out var templateNode) || string.IsNullOrWhiteSpace(templateNode?.ToString()))
        {
            throw new InvalidOperationException("Enabled export definitions must include a 'template'.");
        }

        var overwrite = obj.TryGetPropertyValue("overwrite", out var overwriteNode) && overwriteNode?.GetValue<bool>() == true;
        return new PayloadExportDefinition(true, templateNode!.ToString(), basePath, overwrite);
    }

    private static int ParseCount(JsonNode? node)
    {
        if (node is null)
        {
            return 1;
        }

        if (int.TryParse(node.ToString(), out var value) && value >= 1)
        {
            return value;
        }

        return 1;
    }

    private static int? TryParseInt(JsonObject obj, string property)
    {
        if (obj.TryGetPropertyValue(property, out var node) && node is not null && int.TryParse(node.ToString(), out var value))
        {
            return value;
        }

        return null;
    }

    private static DateTimeOffset? TryParseDateTime(JsonObject obj, string property)
    {
        if (obj.TryGetPropertyValue(property, out var node) && node is not null &&
            DateTimeOffset.TryParse(node.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            return dto;
        }

        return null;
    }

    private static DateOnly? TryParseDateOnly(JsonObject obj, string property)
    {
        if (!obj.TryGetPropertyValue(property, out var node) || node is null)
        {
            return null;
        }

        var raw = node.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var dateOnly))
        {
            return dateOnly;
        }

        // Allow full date-time ISO strings (e.g. 2025-10-01T00:00:00Z) by parsing and trimming the time component.
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            return DateOnly.FromDateTime(dto.UtcDateTime);
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }

        return null;
    }

    private static TimeOnly? TryParseTimeOnly(JsonObject obj, string property)
    {
        if (obj.TryGetPropertyValue(property, out var node) && node is not null &&
            TimeOnly.TryParse(node.ToString(), CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static string? TryParseFormatString(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("format", out var node))
        {
            var value = node?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string ResolveTemplate(string template, PayloadDefinition payload, PayloadProcessorContext context, VariableResolver? resolver)
    {
        return VariableTokenRegex.Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            var format = match.Groups[2].Success ? match.Groups[2].Value : null;
            (token, format) = NormalizeToken(token, format);
            if (TryResolveContext(token, context, out var contextValue))
            {
                return ApplyFormat(contextValue, format);
            }

            if (resolver is not null)
            {
                return resolver.Resolve(token, format);
            }

            throw new InvalidOperationException($"Variable '{token}' is not defined for payload '{Path.GetFileName(payload.Path)}'. Mesage will not be sent.");
        });
    }

    private static string ApplyFormat(string value, string? format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return value;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric.ToString(format, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static string? GetOptionalString(JsonObject obj, string property)
    {
        if (!obj.TryGetPropertyValue(property, out var node))
        {
            return null;
        }

        var candidate = node?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        return candidate.Trim();
    }

    private static (string Token, string? Format) NormalizeToken(string token, string? format)
    {
        if (string.IsNullOrEmpty(format))
        {
            var colonIndex = token.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < token.Length - 1)
            {
                var extracted = token[(colonIndex + 1)..];
                token = token[..colonIndex];
                format = string.IsNullOrWhiteSpace(extracted) ? null : extracted;
            }
        }

        return (token, format);
    }

    private static bool TryResolveContext(string token, PayloadProcessorContext context, out string value)
    {
        value = string.Empty;
        if (context is null || !token.StartsWith("context.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var propertyName = token["context.".Length..];
        switch (propertyName.ToLowerInvariant())
        {
            case "index":
                value = context.Index.ToString(CultureInfo.InvariantCulture);
                return true;
            case "templatefilepath":
                value = context.TemplateFilePath ?? string.Empty;
                return true;
            case "templatefilename":
                value = context.TemplateFileName ?? string.Empty;
                return true;
            case "templatedirectory":
                value = context.TemplateDirectory ?? string.Empty;
                return true;
            case "templatefilenamestem":
            case "templatefilestem":
            case "templatefilenamewithout":
            case "templatefilenamewithoutextension":
            case "templatefilenamenowithextension":
                value = context.TemplateFileNameWithoutExtension ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    private sealed class VariableResolver
    {
        private readonly IReadOnlyDictionary<string, VariableDefinition> _definitions;
        private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _stack = new(StringComparer.OrdinalIgnoreCase);
        private readonly IRandomValueService _valueService;
        private readonly RandomValueFormattingOptions _formatting;
        private readonly PayloadProcessorContext _context;

        public VariableResolver(
            IReadOnlyDictionary<string, VariableDefinition> definitions,
            IRandomValueService valueService,
            RandomValueFormattingOptions formatting,
            PayloadProcessorContext context)
        {
            _definitions = definitions;
            _valueService = valueService;
            _formatting = formatting ?? RandomValueFormattingOptions.Default;
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Resolve(string name, string? format = null)
        {
            var value = ResolveCore(name);
            return ApplyFormat(value, format);
        }

        private string ResolveCore(string name)
        {
            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            if (!_definitions.TryGetValue(name, out var definition))
            {
                throw new InvalidOperationException($"Variable '{name}' is not defined.");
            }

            if (!_stack.Add(name))
            {
                throw new InvalidOperationException($"Circular variable reference detected for '{name}'.");
            }

            string value;
            if (definition.RandomDefinition is not null)
            {
                value = _valueService.Generate(definition.RandomDefinition, _context, _formatting);
            }
            else if (definition.Template is not null)
            {
                value = VariableTokenRegex.Replace(definition.Template, match =>
                {
                    var token = match.Groups[1].Value;
                    var format = match.Groups[2].Success ? match.Groups[2].Value : null;
                    (token, format) = NormalizeToken(token, format);
                    if (TryResolveContext(token, _context, out var contextValue))
                    {
                        return ApplyFormat(contextValue, format);
                    }

                    return Resolve(token, format);
                });
            }
            else
            {
                value = string.Empty;
            }

            _stack.Remove(name);
            _cache[name] = value;
            return value;
        }
    }
}
