using System.Text.Json;

namespace GenesysAudioHookServer.Types;

internal class Messageheader
{
    public string? version { get; set; } = "2";
    public string? id { get; set; }
    public string? type { get; set; }
    public int seq { get; set; }
  
    public JsonObject? parameters { get; set; }
}

public class JsonObject : Dictionary<string, object>
{
    /// <summary>
    /// Safely retrieves the value associated with the specified key and casts or deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="key">The key to look for in the collection.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist or the cast/deserialization fails.</param>
    /// <returns>The value associated with the key, cast or deserialized to the specified type, or the default value if the key does not exist or the operation fails.</returns>
    public T? GetValueOrDefault<T>(string key, T? defaultValue = default)
    {
        if (this.TryGetValue(key, out var value))
        {
            // If the value is already of the desired type, return it directly
            if (value is T typedValue)
            {
                return typedValue;
            }

            // If the value is a JsonElement, attempt to deserialize it
            if (value is JsonElement jsonElement)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                catch (JsonException)
                {
                    // Log or handle deserialization failure if needed
                    return defaultValue;
                }
            }
        }

        return defaultValue;
    }
}
