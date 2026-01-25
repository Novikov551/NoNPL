using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace NoNPL.Entities
{
    /// <summary> Претокен </summary>
    [DebuggerDisplay("b{UTF8Value}: [{string.Join(\", \", Tokens.Values)}]")]
    public class PreToken : IEquatable<PreToken>
    {
        private static readonly Encoding _utf8WithoutBOM = new UTF8Encoding(false);

        public PreToken(string preTokenValue)
        {
            // Быстрая проверка на BOM
            if (preTokenValue.Length > 0 && preTokenValue[0] == '\uFEFF')
            {
                UTF8Value = preTokenValue.Substring(1);
            }
            else
            {
                UTF8Value = preTokenValue;
            }

            Bytes = _utf8WithoutBOM.GetBytes(preTokenValue);

            _hashCode = CalculateHashCode(Bytes);

            if (Bytes.Length == 0) return;

            Tokens = new Dictionary<int, Token>(Bytes.Length);
            if (Bytes.Length == 1)
            {
                Tokens[0] = SinglByteTokenCache.Tokens[Bytes[0]];
            }
            else
            {
                for (int i = 0; i < Bytes.Length; i++)
                {
                    Tokens[i] = SinglByteTokenCache.Tokens[Bytes[i]];
                }
            }
        }

        public byte[] Bytes { get; init; }
        public Dictionary<int, Token> Tokens { get; private set; }
        public string UTF8Value { get; init; }
        private readonly int _hashCode;

        private static int CalculateHashCode(byte[] bytes)
        {
            if (bytes.Length == 0) return 0;

            unchecked
            {
                int hash = 17;
                // Обрабатываем по 4 байта за раз если возможно
                int i = 0;
                for (; i <= bytes.Length - 4; i += 4)
                {
                    hash = hash * 31 + bytes[i];
                    hash = hash * 31 + bytes[i + 1];
                    hash = hash * 31 + bytes[i + 2];
                    hash = hash * 31 + bytes[i + 3];
                }

                // Оставшиеся байты
                for (; i < bytes.Length; i++)
                {
                    hash = hash * 31 + bytes[i];
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
