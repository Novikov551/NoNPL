using System.Diagnostics;
using System.Text;

namespace NoNPL.Entities
{
    /// <summary> Претокен </summary>
    [DebuggerDisplay("b'{UTF8Value}': {string.Join(\", \", Bytes)}")]
    public class PreToken : IEquatable<PreToken>
    {
        public PreToken(string preTokenValue)
        {
            Tokens = new();
            UTF8Value = preTokenValue;
            Bytes = Encoding.UTF8.GetBytes(preTokenValue);

            var bytePairNumber = 0;
            if (Bytes.Count() > 1)
            {
                for (var i = 0; i < Bytes.Count(); i++)
                {
                    Tokens.Add(bytePairNumber, new Token([Bytes[i]]));
                    bytePairNumber++;
                }
            }
            else
            {
                Tokens.Add(bytePairNumber, new Token(Bytes));
            }
        }

        public byte[] Bytes { get; set; }
        public Dictionary<int, Token> Tokens { get; private set; }
        public string UTF8Value { get; private set; }

        public bool Equals(PreToken other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;

            return Tokens.Values.SequenceEqual(other.Tokens.Values);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PreToken);
        }

        public override int GetHashCode()
        {
            // Вычисляем хэш-код на основе всех токенов
            unchecked
            {
                int hash = 17;
                foreach (var token in Tokens.Values.OrderBy(t => t.GetHashCode()))
                {
                    hash = hash * 31 + token.GetHashCode();
                }
                return hash;
            }
        }
    }
}
