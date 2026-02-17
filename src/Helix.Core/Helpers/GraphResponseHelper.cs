using System.Text.Json;
using System.Text.Json.Nodes;

namespace Helix.Core.Helpers;

public static class GraphResponseHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Formats a Graph API response by serializing to JSON and stripping OData metadata properties.
    /// </summary>
    public static string FormatResponse(object? data)
    {
        if (data is null)
            return JsonSerializer.Serialize(new { success = true }, SerializerOptions);

        var json = JsonSerializer.Serialize(data, SerializerOptions);
        return StripODataProperties(json);
    }

    private static string StripODataProperties(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                StripODataFromObject(obj);
                return obj.ToJsonString(SerializerOptions);
            }
            return json;
        }
        catch
        {
            return json;
        }
    }

    private static void StripODataFromObject(JsonObject obj)
    {
        var keysToRemove = obj
            .Where(kvp => kvp.Key.StartsWith("@odata.", StringComparison.OrdinalIgnoreCase)
                       || kvp.Key.StartsWith("odata.", StringComparison.OrdinalIgnoreCase)
                       || kvp.Key is "backingStore" or "odataType" or "additionalData")
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
            obj.Remove(key);

        foreach (var kvp in obj)
        {
            if (kvp.Value is JsonObject childObj)
                StripODataFromObject(childObj);
            else if (kvp.Value is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JsonObject arrObj)
                        StripODataFromObject(arrObj);
                }
            }
        }
    }
}
