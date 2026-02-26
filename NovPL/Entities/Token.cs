using System.Buffers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MessagePack;
using System.Text.Json.Serialization;

namespace NoNPL.Entities
{
    /// <summary> Токен </summary>
    [DebuggerDisplay("b'{UTF8Value}': [{string.Join(',', Bytes)}]")]
    [MessagePackObject(AllowPrivate = true)]
    public class Token : IEquatable<Token>
    {
        public Token(byte[] bytes)
        {
            Bytes = bytes;

            UTF8Value = Encoding.UTF8.GetString(Bytes);

            _hashCode = CalculateHashCode();
        }

        private Token() { }

        public Token(TokenPair frequensedPair)
        {
            // Используем ArrayPool для снижения нагрузки на GC
            var result = ArrayPool<byte>.Shared.Rent(
                frequensedPair.First.Bytes.Length + frequensedPair.Second.Bytes.Length);

            try
            {
                frequensedPair.First.Bytes.CopyTo(result, 0);
                frequensedPair.Second.Bytes.CopyTo(result, frequensedPair.First.Bytes.Length);

                Bytes = new byte[frequensedPair.First.Bytes.Length + frequensedPair.Second.Bytes.Length];
                Array.Copy(result, Bytes, Bytes.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(result);
            }

            UTF8Value = Encoding.UTF8.GetString(Bytes);
        }

        [JsonPropertyName("bytes")]
        [Key(0)]
        public byte[] Bytes { get; private set; }

        [IgnoreMember]
        [JsonIgnore]  
        public string UTF8Value { get; init; }

        [JsonIgnore]
        [IgnoreMember]
        private readonly int _hashCode;

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

        public int CalculateHashCode()
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

        public override int GetHashCode() => _hashCode;

        public override string ToString()
        {
            return $"b'{UTF8Value}':[{string.Join(", ", Bytes)}]";
        }
    }
}
