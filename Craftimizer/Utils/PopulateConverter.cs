using Newtonsoft.Json;
using System;

namespace Craftimizer.Utils;

public class PopulateConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) =>
        objectType.GetConstructor(Type.EmptyTypes) != null;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        existingValue ??= Activator.CreateInstance(objectType) ?? throw new ArgumentException($"Could not create object of type {objectType}", nameof(objectType));
        serializer.Populate(reader, existingValue);
        return existingValue;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}
