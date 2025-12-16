using SendMessageToExchange.Definitions;

namespace SendMessageToExchange.Services.Interfaces;

public interface IPayloadExportService
{
    void Export(PayloadDefinition payload, PayloadProcessorContext context, string content, Func<string, string> formatResolver);
}
