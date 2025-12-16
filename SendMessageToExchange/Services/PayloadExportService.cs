using System.Text;

using SendMessageToExchange.Definitions;
using SendMessageToExchange.Services.Interfaces;

namespace SendMessageToExchange.Services;

public sealed class PayloadExportService : IPayloadExportService
{
    public void Export(
        PayloadDefinition payload,
        PayloadProcessorContext context,
        string content,
        Func<string, string> formatResolver)
    {
        if (payload.Export is not { Enabled: true, Template: { Length: > 0 } template } exportDefinition)
        {
            return;
        }

        var resolvedTemplate = formatResolver(template);
        if (string.IsNullOrWhiteSpace(resolvedTemplate))
        {
            throw new InvalidOperationException($"Export template resolved to an empty path for payload '{Path.GetFileName(payload.Path)}'.");
        }

        var finalPath = Path.IsPathRooted(resolvedTemplate)
            ? resolvedTemplate
            : Path.GetFullPath(resolvedTemplate, exportDefinition.BasePath);
        var directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!exportDefinition.Overwrite && File.Exists(finalPath))
        {
            throw new IOException($"Export file '{finalPath}' already exists and overwrite is disabled.");
        }

        File.WriteAllText(finalPath, content, Encoding.UTF8);
    }
}
