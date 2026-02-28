using NoNPL.Entities;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoNPL.Converters
{
    public class TokenJsonConverter : JsonConverter<Token>
    {
        public override Token Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            byte[] bytes = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = reader.GetString();
                    reader.Read();

                    switch (propName)
                    {
                        case "bytes":
                            // Ожидаем массив чисел
                            if (reader.TokenType != JsonTokenType.StartArray)
                                throw new JsonException("Expected start of array for bytes");
                            var list = new List<byte>();
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType == JsonTokenType.Number)
                                    list.Add(reader.GetByte());
                                else
                                    throw new JsonException("Expected number in bytes array");
                            }
                            bytes = list.ToArray();
                            break;
                        case "UTF8Value":
                            reader.Skip();
                            break;
                    }
                }
            }

            if (bytes == null)
                throw new JsonException("Missing bytes property");

            return new Token(bytes);
        }

        public override void Write(Utf8JsonWriter writer, Token value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("bytes");
            writer.WriteStartArray();
            foreach (byte b in value.Bytes)
            {
                writer.WriteNumberValue(b);
            }
            writer.WriteEndArray();
            writer.WriteString("UTF8Value", value.UTF8Value);
            writer.WriteEndObject();
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, Token value, JsonSerializerOptions options)
        {
            string base64 = Convert.ToBase64String(value.Bytes);
            writer.WritePropertyName(base64);
        }

        public override Token ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string base64 = reader.GetString();
            byte[] bytes = Convert.FromBase64String(base64);
            return new Token(bytes);
        }
    }

    public class TokenPairJsonConverter : JsonConverter<TokenPair>
    {
        public override TokenPair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            Token first = null;
            Token second = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = reader.GetString();
                    reader.Read();

                    switch (propName)
                    {
                        case "first":
                            first = JsonSerializer.Deserialize<Token>(ref reader, options);
                            break;
                        case "second":
                            second = JsonSerializer.Deserialize<Token>(ref reader, options);
                            break;
                    }
                }
            }

            if (first == null || second == null)
                throw new JsonException("Missing first or second");

            return new TokenPair(first, second);
        }

        public override void Write(Utf8JsonWriter writer, TokenPair value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("first");
            JsonSerializer.Serialize(writer, value.First, options);
            writer.WritePropertyName("second");
            JsonSerializer.Serialize(writer, value.Second, options);
            writer.WriteEndObject();
        }
    }

    public class ConcurrentDictionaryJsonConverter<TKey, TValue> : JsonConverter<ConcurrentDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override ConcurrentDictionary<TKey, TValue> Read(ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options);
            return dict != null ? new ConcurrentDictionary<TKey, TValue>(dict) : new ConcurrentDictionary<TKey, TValue>();
        }

        public override void Write(Utf8JsonWriter writer,
            ConcurrentDictionary<TKey, TValue> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer,
                value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                options);
        }
    }
}