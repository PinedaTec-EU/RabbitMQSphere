using System.Globalization;

using SendMessageToExchange.Definitions;
using SendMessageToExchange.Services.Interfaces;

namespace SendMessageToExchange.Services;

public sealed class DynamicValueService : IRandomValueService
{
    private const int DefaultTextLength = 16;

    public string Generate(RandomValueDefinition definition, PayloadProcessorContext context, RandomValueFormattingOptions formatting)
    {
        formatting ??= RandomValueFormattingOptions.Default;

        return definition.Type switch
        {
            RandomValueType.Number => FormatNumber(GenerateRandomNumber(definition.Min ?? 1, definition.Max ?? 100), definition.Padding),
            RandomValueType.Text => GenerateRandomText(definition.Length ?? DefaultTextLength),
            RandomValueType.Guid => Guid.NewGuid().ToString(),
            RandomValueType.Ulid => Ulid.NewUlid().ToString(),
            RandomValueType.DateTime => GenerateRandomDateTime(definition.FromDateTime, definition.ToDateTime)
                .ToString(ResolveFormat(definition.Format, formatting.DateTimeFormat), CultureInfo.InvariantCulture),
            RandomValueType.Date => GenerateRandomDate(definition.FromDate, definition.ToDate)
                .ToString(ResolveFormat(definition.Format, formatting.DateFormat), CultureInfo.InvariantCulture),
            RandomValueType.Time => GenerateRandomTime(definition.FromTime, definition.ToTime)
                .ToString(ResolveFormat(definition.Format, formatting.TimeFormat), CultureInfo.InvariantCulture),
            RandomValueType.Sequence => GenerateSequenceValue(definition, context),
            _ => throw new InvalidOperationException("Unsupported variable type.")
        };
    }

    private static int GenerateRandomNumber(int min, int max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        if (max == int.MaxValue)
        {
            var value = Random.Shared.NextInt64(min, (long)max + 1);
            return (int)value;
        }

        return Random.Shared.Next(min, max + 1);
    }

    private static string GenerateRandomText(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> buffer = length <= 64 ? stackalloc char[length] : new char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = chars[Random.Shared.Next(chars.Length)];
        }

        return new string(buffer);
    }

    private static DateTimeOffset GenerateRandomDateTime(DateTimeOffset? from, DateTimeOffset? to)
    {
        var start = from ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var end = to ?? DateTimeOffset.UtcNow.AddMonths(1);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var offsetTicks = NextInt64Inclusive(0, end.Ticks - start.Ticks);
        return start.AddTicks(offsetTicks);
    }

    private static DateOnly GenerateRandomDate(DateOnly? from, DateOnly? to)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var offsetDays = Random.Shared.Next(end.DayNumber - start.DayNumber + 1);
        return start.AddDays(offsetDays);
    }

    private static TimeOnly GenerateRandomTime(TimeOnly? from, TimeOnly? to)
    {
        var start = from ?? TimeOnly.MinValue;
        var end = to ?? TimeOnly.MaxValue;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var offsetTicks = NextInt64Inclusive(0, end.Ticks - start.Ticks);
        return start.AddMinutes(offsetTicks);
    }

    private static long NextInt64Inclusive(long min, long max)
    {
        if (max <= min)
        {
            return min;
        }

        var range = max - min;
        var offset = Random.Shared.NextInt64(range + 1);
        return min + offset;
    }

    private static string FormatNumber(long value, int? padding)
    {
        if (padding is >= 1)
        {
            var format = $"D{padding}";
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string GenerateSequenceValue(RandomValueDefinition definition, PayloadProcessorContext context)
    {
        if (context is null)
        {
            throw new InvalidOperationException("Sequence variables require a valid context.");
        }

        long start = definition.Start ?? 1;
        long step = Math.Max(1, definition.Step ?? 1);
        var value = checked(start + (context.Index - 1) * step);
        return FormatNumber(value, definition.Padding);
    }

    private static string ResolveFormat(string? overrideFormat, string fallback)
    {
        return string.IsNullOrWhiteSpace(overrideFormat) ? fallback : overrideFormat!;
    }
}
