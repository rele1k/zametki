using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zametki.Services;

/// <summary>
/// Разбор ответов tRPC, закодированных superjson.
/// Ответ батча — всегда массив: [ { "result": { "data": { "json": ..., "meta": { "values": {...} } } } }, ... ]
/// либо элемент с "error" вместо "result" при ошибке процедуры.
/// </summary>
public static class SuperJsonLite
{
    public class TrpcException : Exception
    {
        public TrpcException(string message) : base(message) { }
    }

    /// <summary>Достаёт data.json первого элемента батча, восстанавливая даты по meta.values.</summary>
    public static JsonElement GetData(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new TrpcException("Пустой или неверный ответ сервера");
        return Extract(root[0]);
    }

    private static JsonElement Extract(JsonElement item)
    {
        if (item.TryGetProperty("error", out var err))
        {
            var msg = err.TryGetProperty("json", out var ej) && ej.TryGetProperty("message", out var m)
                ? m.GetString()
                : null;
            throw new TrpcException(string.IsNullOrEmpty(msg) ? "Ошибка сервера" : msg);
        }

        var data = item.GetProperty("result").GetProperty("data");
        var json = data.GetProperty("json");

        if (data.TryGetProperty("meta", out var meta) &&
            meta.TryGetProperty("values", out var values) &&
            values.ValueKind == JsonValueKind.Object)
        {
            json = RestoreDates(json, values);
        }
        return json.Clone();
    }

    /// <summary>
    /// superjson помечает даты в meta.values по пути, напр. "0.createdAt": ["Date"].
    /// Строка даты уже лежит в самом поле, поэтому для типа "Date" достаточно убедиться,
    /// что Json-десериализация в DateTimeOffset пройдёт (ISO-формат парсится нативно).
    /// Формат { "value": ..., "type": "Date" } тоже поддерживаем: подставляем value на место поля.
    /// </summary>
    private static JsonElement RestoreDates(JsonElement json, JsonElement values)
    {
        var node = JsonNode.Parse(json.GetRawText());
        foreach (var prop in values.EnumerateObject())
        {
            string? raw = null;
            if (prop.Value.ValueKind == JsonValueKind.Object &&
                prop.Value.TryGetProperty("type", out var t) &&
                t.GetString() == "Date" &&
                prop.Value.TryGetProperty("value", out var v))
            {
                raw = v.GetRawText();
            }
            else
            {
                continue;
            }

            var segments = prop.Name.Split('.');
            JsonNode? cur = node;
            foreach (var seg in segments.SkipLast(1))
            {
                if (cur is null) break;
                cur = int.TryParse(seg, out var idx) && cur is JsonArray arr && idx < arr.Count
                    ? arr[idx]
                    : cur[seg];
            }
            if (cur is null) continue;
            var last = segments[^1];
            var replacement = JsonNode.Parse(raw);
            if (cur is JsonArray arr2 && int.TryParse(last, out var i2) && i2 < arr2.Count)
                arr2[i2] = replacement;
            else
                cur[last] = replacement;
        }
        using var doc = JsonDocument.Parse(node!.ToJsonString());
        return doc.RootElement.Clone();
    }
}
