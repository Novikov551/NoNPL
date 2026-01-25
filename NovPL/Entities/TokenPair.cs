namespace NoNPL.Entities
{
    // Если определение структуры уместно в вашем контексте:
    public readonly struct TokenPair : IEquatable<TokenPair>
    {
        public readonly Token First;
        public readonly Token Second;

        public TokenPair(Token first, Token second) => (First, Second) = (first, second);

        public bool Equals(TokenPair other) =>
            EqualityComparer<Token>.Default.Equals(First, other.First) &&
            EqualityComparer<Token>.Default.Equals(Second, other.Second);

        public override bool Equals(object obj) => obj is TokenPair other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(First?.GetHashCode() ?? 0, Second?.GetHashCode() ?? 0);
    }
}
