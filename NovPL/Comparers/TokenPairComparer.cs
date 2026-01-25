using NoNPL.Entities;

namespace NoNPL.Comparers
{
    public class TokenPairComparer : IEqualityComparer<(Token, Token)>
    {
        public static readonly TokenPairComparer Instance = new();

        public bool Equals((Token, Token) x, (Token, Token) y)
        {
            return Equals(x.Item1, y.Item1) && Equals(x.Item2, y.Item2);
        }

        private static bool Equals(Token a, Token b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;

            // Оптимизация: сначала сравнить хеши
            if (a.GetHashCode() != b.GetHashCode()) return false;

            return a.Equals(b);
        }

        public int GetHashCode((Token, Token) obj)
        {
            // Самый производительный вариант для .NET Core 2.1+
            return HashCode.Combine(obj.Item1, obj.Item2);
        }
    }
}
