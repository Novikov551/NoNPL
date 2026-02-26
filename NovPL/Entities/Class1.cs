using MessagePack;
using System.Collections.Concurrent;

namespace NoNPL.Entities
{
    [MessagePackObject]
    public class TokenData
    {
        [Key(0)]
        public ConcurrentDictionary<Token, int> Vocab { get; set; } = new();

        [Key(1)]
        public List<TokenPair> Merges { get; set; } = new();

        [Key(2)]
        public string Version { get; set; } = "1.0";
    }
}
