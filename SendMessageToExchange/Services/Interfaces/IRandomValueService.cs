using SendMessageToExchange.Definitions;

namespace SendMessageToExchange.Services.Interfaces;

public interface IRandomValueService
{
    string Generate(RandomValueDefinition definition, PayloadProcessorContext context, RandomValueFormattingOptions formatting);
}
