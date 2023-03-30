using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethermind.Serialization.Json;

public static class JsonHelperExtensions
{
    public static JToken GetRequired(this JObject obj, string name)
    {
        return obj[name] ?? throw new JsonSerializationException($"Required field {name} is null");
    }

    public static T ToObjectRequired<T>(this JToken tok, JsonSerializer serializer)
    {
        return tok.ToObject<T>(serializer) ?? throw new JsonSerializationException($"Couldn't deserialize {tok.Path}");
    }
}