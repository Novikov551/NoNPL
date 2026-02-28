using MessagePack;
using System;
using System.Text.Json.Serialization;

namespace NoNPL.Entities
{
    [MessagePackObject]
    public readonly struct TokenPair : IEquatable<TokenPair>
    {
        [JsonPropertyName("first")]
        [Key(0)]
        public readonly Token First;

        [JsonPropertyName("second")]
        [Key(1)]
        public readonly Token Second;

        [IgnoreMember]
        private readonly int _hashCode;

        public TokenPair(Token first, Token second) 
        {
            First = first;
            Second = second;

            _hashCode = HashCode.Combine(First?.GetHashCode() ?? 0, Second?.GetHashCode() ?? 0);
        }
        
        public bool Equals(TokenPair other) =>
            EqualityComparer<Token>.Default.Equals(First, other.First) &&
            EqualityComparer<Token>.Default.Equals(Second, other.Second);

        public override bool Equals(object obj) => obj is TokenPair other && Equals(other);

        public override int GetHashCode() => _hashCode;

        public override string ToString()
        {
            return $"b'{First.UTF8Value} {Second.UTF8Value}':[{string.Join(", ", First.Bytes)},{string.Join(", ", Second.Bytes)}]";
        }
    }
}
