using NoNPL.Entities;
using System.Collections.Concurrent;

namespace NoNPL.Services.Serializers
{
    public interface ISerializer
    {
        Task SaveAsync(string filePath, ConcurrentDictionary<Token, int> vocab, List<TokenPair> merges, CancellationToken cancellationToken = default);
        Task<(ConcurrentDictionary<Token, int> Vocab, List<TokenPair> Merges)> LoadAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
