using System.Diagnostics;

namespace NoNPL.Entities
{
    /// <summary> Токен </summary>
    [DebuggerDisplay("{string.Join(',', Bytes)}")]
    public class Token : IEquatable<Token>
    {
        public Token(byte[] bytes)
        {
            Bytes = bytes;
        }

        public byte[] Bytes { get; private set; }

        public bool Equals(Token other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;

            return Bytes.SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Token);
        }

        public override int GetHashCode()
        {
            // Простое вычисление хэш-кода для массива байт
            unchecked
            {
                int hash = 17;
                foreach (byte b in Bytes)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }
    }
}
