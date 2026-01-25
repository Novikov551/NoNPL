using System;

namespace NoNPL.Entities
{
    public readonly struct TokenPair : IEquatable<TokenPair>
    {
        public readonly Token First;
        public readonly Token Second;
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
