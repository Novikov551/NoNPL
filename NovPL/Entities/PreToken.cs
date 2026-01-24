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
        public PreToken(string preTokenValue)
        {
            Tokens = new();
            UTF8Value = preTokenValue;
            Bytes = Encoding.UTF8.GetBytes(preTokenValue);

            var tokenNumber = 0;
            if (Bytes.Count() > 1)
            {
                for (var i = 0; i < Bytes.Count(); i++)
                {
                    Tokens.Add(tokenNumber, new Token([Bytes[i]]));
                    tokenNumber++;
                }
            }
            else
            {
                Tokens.Add(tokenNumber, new Token(Bytes));
            }
        }

        public byte[] Bytes { get; init; }
        public Dictionary<int, Token> Tokens { get; private set; }
        public string UTF8Value { get; init; }

        public bool Equals(PreToken other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;

            return Bytes.SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PreToken);
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

        internal (List<(Token First, Token Second)> NewPairs, List<(Token First, Token Second)> NeedUpdateCounterPairs) MergePair((Token First, Token Second) frequensedPair)
        {
            var newToken = new Token(frequensedPair);

            var firstIndex = 0;
            var secondIndex = 1;

            foreach (var token in Tokens)
            {
                var firstEqual = token.Value.Equals(frequensedPair.First);
                if (firstEqual)
                {
                    if (token.Key < Tokens.Max(e => e.Key))
                    {
                        var secondEqual = Tokens[token.Key + 1].Equals(frequensedPair.Second);
                        if (secondEqual)
                        {
                            firstIndex = token.Key;
                            secondIndex = token.Key + 1;

                            break;
                        }
                    }
                }
            }

            var firstToken = Tokens[firstIndex];
            var secondToken = Tokens[secondIndex];

            var newDict = new Dictionary<int, Token>(Tokens.Count - 1);

            foreach (var token in Tokens)
            {
                if (token.Key != firstIndex)
                {
                    if (token.Key < secondIndex)
                    {
                        newDict.Add(token.Key, token.Value);
                    }

                    if (token.Key > secondIndex)
                    {
                        newDict.Add(token.Key - 1, token.Value);
                    }
                }
            }

            if (firstIndex == 5) { }
            Tokens = newDict;
            Tokens.Add(firstIndex, newToken);

            // Находим новые пары
            var newPairs = new List<(Token First, Token Second)>();

            // Находим пары у которых нужно обновить счетчики
            var needUpdatePairs = new List<(Token First, Token Second)>();

            var leftNumber = -1;
            if (firstIndex != 0)
            {
                leftNumber = firstIndex - 1;
            }

            if (leftNumber != -1)
            {
                newPairs.Add((Tokens[leftNumber], newToken));
                needUpdatePairs.Add((Tokens[leftNumber], frequensedPair.First));
            }

            var rightNumber = -1;
            if (secondIndex <= Tokens.Max(e => e.Key))
            {
                rightNumber = secondIndex;//потому что мы уже удалили второй токен и заменили его последующим
            }

            if (rightNumber != -1)
            {
                newPairs.Add((newToken, Tokens[rightNumber]));
                needUpdatePairs.Add((frequensedPair.Second, Tokens[rightNumber]));
            }

            return (newPairs, needUpdatePairs);
        }
    }
}
