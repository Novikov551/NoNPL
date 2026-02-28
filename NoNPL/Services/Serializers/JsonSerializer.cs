using NoNPL.Converters;
using NoNPL.Entities;
using System.Collections.Concurrent;
using System.Text.Json;

namespace NoNPL.Services.Serializers
{
    public class JsonSerializer : ISerializer
    {
        private readonly JsonSerializerOptions _options;

        public JsonSerializer(bool writeIndented = true)
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = writeIndented,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters =
                {
                    new TokenJsonConverter(), 
                    new TokenPairJsonConverter(),
                    new ConcurrentDictionaryJsonConverter<Token, int>()
                }
            };
        }

        public async Task SaveAsync(string filePath, ConcurrentDictionary<Token, int> vocab, List<TokenPair> merges, CancellationToken cancellationToken = default)
        {
            var data = new TokenData { Vocab = vocab, Merges = merges };
            await using var stream = File.Create(filePath);
            await System.Text.Json.JsonSerializer.SerializeAsync(stream, data, _options, cancellationToken);
        }

        public async Task<(ConcurrentDictionary<Token, int> Vocab, List<TokenPair> Merges)> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(filePath);
            var data = await System.Text.Json.JsonSerializer.DeserializeAsync<TokenData>(stream, _options, cancellationToken);
            return (data?.Vocab ?? new ConcurrentDictionary<Token, int>(), data?.Merges ?? new List<TokenPair>());
        }
    }
}
