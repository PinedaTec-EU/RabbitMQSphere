using System.Text.Json.Nodes;
using SendMessageToExchange.Definitions;

namespace SendMessageToExchange.Processors.Interfaces;

public interface IPayloadBuilderProcessor
{
    PayloadDefinition[] Process(JsonObject config, string basePath);
    string BuildPayloadText(PayloadDefinition payload, PayloadProcessorContext context);
    ReadOnlyMemory<byte> BuildPayloadBody(PayloadDefinition payload, PayloadProcessorContext context);
}
