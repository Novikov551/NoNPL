using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NoNPL.Entities
{
    /// <summary> Токен </summary>
    [DebuggerDisplay("b'{UTF8Value}': [{string.Join(',', Bytes)}]")]
    public class Token : IEquatable<Token>
    {
        public Token(byte[] bytes)
        {
            Bytes = bytes;

            UTF8Value = Encoding.UTF8.GetString(Bytes);
        }

        public Token((Token First, Token Second) frequensedPair)
        {
            // Создаем массив нужного размера
            var result = new byte[frequensedPair.First.Bytes.Length + frequensedPair.Second.Bytes.Length];

            // Копируем первый массив в начало результата
            Buffer.BlockCopy(frequensedPair.First.Bytes, 0, result, 0, frequensedPair.First.Bytes.Length);

            // Копируем второй массив после первого
            Buffer.BlockCopy(frequensedPair.Second.Bytes, 0, result, frequensedPair.First.Bytes.Length, frequensedPair.Second.Bytes.Length);

            Bytes = result;

            UTF8Value = Encoding.UTF8.GetString(Bytes);
        }

        public byte[] Bytes { get; private set; }

        public string UTF8Value { get; init; }

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

        public override string ToString()
        {
            return $"b'{UTF8Value}':[{string.Join(", ", Bytes)}]";
        }
    }
}
