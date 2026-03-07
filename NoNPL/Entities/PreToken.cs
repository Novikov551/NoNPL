using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace NoNPL.Entities
{
    /// <summary> Претокен </summary>
    public class PreToken : IEquatable<PreToken>
    {
        public PreToken(ReadOnlySpan<char> preTokenValue)
        {
            Bytes = UTF8Converter.GetBytes(preTokenValue);

            if (Bytes.Length == 0)
            {
                Tokens = new Dictionary<int, Token>(0);
                _hashCode = 0;
                return;
            }

            Tokens = new Dictionary<int, Token>(Bytes.Length);
            
            _hashCode = CalculateHashCode();
        }

        public byte[] Bytes { get; init; }
        public Dictionary<int, Token> Tokens { get; private set; }
        private readonly int _hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < Bytes.Length; i++)
                {
                    byte b = Bytes[i];
                    hash = hash * 31 + b;
                    Tokens[i] = SinglByteTokenCache.Tokens[b];
                }
                return hash;
            }
        }

        public bool Equals(PreToken other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;

            // Быстрое сравнение по хэш-коду
            if (_hashCode != other._hashCode) return false;

            // Используем Span для быстрого сравнения массивов
            return Bytes.AsSpan().SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PreToken);
        }

        public override int GetHashCode() => _hashCode;

        internal (List<TokenPair> NewPairs, List<TokenPair> NeedUpdateCounterPairs) MergePair(TokenPair frequensedPair)
        {
            var newToken = new Token(frequensedPair);

            var firstIndex = -1;
            var tokensList = Tokens.OrderBy(kv => kv.Key).ToList();

            for (int i = 0; i < tokensList.Count - 1; i++)
            {
                if (tokensList[i].Value.Equals(frequensedPair.First) &&
                    tokensList[i + 1].Value.Equals(frequensedPair.Second))
                {
                    firstIndex = tokensList[i].Key;
                    break;
                }
            }

            if (firstIndex == -1)
                return (new List<TokenPair>(), new List<TokenPair>());

            var secondIndex = firstIndex + 1;
            var newDict = new Dictionary<int, Token>(Tokens.Count - 1);

            var pairReplaced = false;
            foreach (var token in tokensList)
            {
                if (token.Key == firstIndex)
                {
                    newDict[token.Key] = newToken;
                    pairReplaced = true;
                    continue;
                }
                else if (token.Key == secondIndex)
                {
                    continue; // Пропускаем второй токен пары
                }

                // Корректируем индекс если пара уже заменена
                int newKey = pairReplaced && token.Key > secondIndex ? token.Key - 1 : token.Key;
                newDict[newKey] = token.Value;
            }

            Tokens = newDict;

            var newPairs = new List<TokenPair>();
            var needUpdatePairs = new List<TokenPair>();

            // Левая пара
            if (firstIndex > 0 && Tokens.TryGetValue(firstIndex - 1, out var leftToken))
            {
                newPairs.Add(new TokenPair(leftToken, newToken));
                needUpdatePairs.Add(new TokenPair(leftToken, frequensedPair.First));
            }

            // Правая пара
            if (Tokens.TryGetValue(firstIndex + 1, out var rightToken))
            {
                newPairs.Add(new TokenPair(newToken, rightToken));
                needUpdatePairs.Add(new TokenPair(frequensedPair.Second, rightToken));
            }

            return (newPairs, needUpdatePairs);
        }
    }
}
