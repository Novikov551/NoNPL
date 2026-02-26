using MessagePack;
using NoNPL.Entities;
using System.Collections.Concurrent;

namespace NoNPL.Services.Serializers
{
    public class MessagePackSerializer : ISerializer
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackSerializer()
        {
            _options = MessagePackSerializerOptions.Standard
                .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance); // позволяет сериализовать без атрибутов, но мы уже их добавили
        }

        public async Task SaveAsync(string filePath, ConcurrentDictionary<Token, int> vocab, List<TokenPair> merges, CancellationToken cancellationToken = default)
        {
            var data = new TokenData { Vocab = vocab, Merges = merges };
            await using var stream = File.Create(filePath);
            await MessagePack.MessagePackSerializer.SerializeAsync(stream, data, _options, cancellationToken);
        }

        public async Task<(ConcurrentDictionary<Token, int> Vocab, List<TokenPair> Merges)> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(filePath);
            var data = await MessagePack.MessagePackSerializer.DeserializeAsync<TokenData>(stream, _options, cancellationToken);
            return (data?.Vocab ?? new ConcurrentDictionary<Token, int>(), data?.Merges ?? new List<TokenPair>());
        }
    }
}
