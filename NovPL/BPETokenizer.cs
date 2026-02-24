using BenchmarkDotNet.Attributes;
using NoNPL.Comparers;
using NoNPL.DataReaders;
using NoNPL.Entities;
using NoNPL.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NoNPL;

[MemoryDiagnoser]
[RankColumn]
public class BPETokenizer
{
    private List<TokenPair> _merges = [];

    private ConcurrentDictionary<Token, int> _vocab;
    private ConcurrentDictionary<PreToken, int> _preTokens;
    private ConcurrentDictionary<TokenPair, HashSet<PreToken>> _tokenPairsHashSet;
    private ConcurrentDictionary<TokenPair, int> _tokenPairsCount;

    private readonly Regex _pattern;
    private readonly TXTDatasetReader _tXTDatasetReader;

    public BPETokenizer(string pattern)
    {
        _pattern = new Regex(pattern, RegexOptions.Compiled);
        _vocab = new();
        _merges = new();
        _preTokens = new();

        _tokenPairsHashSet = new();
        _tokenPairsCount = new();
        _tXTDatasetReader = new();
    }

    public async Task Train(string filePath, string tokenSeparator, int vocabSize, int maxConcurrent = 12)
    {
        var stopwatch = Stopwatch.StartNew();

        InitVocabularity([tokenSeparator]);

        var chunks = await _tXTDatasetReader.ReadTXTDatasetAsync(filePath, tokenSeparator, maxConcurrent);

        ProcessChunks(chunks, tokenSeparator);

        var result = true;
        while (result && _vocab.Count <= vocabSize)
        {
            result = MergePairs();
        }

        var maxId = _vocab.Max(e => e.Value);

        var bytes = Encoding.UTF8.GetBytes(tokenSeparator);
        var token = new Token(bytes);
        _vocab.TryAdd(token, maxId + 1);

        Console.WriteLine($"Датасет обработан за: {stopwatch.Elapsed:mm\\:ss\\.ff}\n");

        stopwatch.Stop();
    }

    public async Task<List<int>> Encode(string text)
    {
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine($"Старт токенизации.");

        var tokens = new List<int>(text.Length);
        foreach (ValueMatch match in _pattern.EnumerateMatches(text))
        {
            var matchValue = text.AsSpan(match.Index, match.Length);
            var preToken = new PreToken(matchValue.ToString());

            if (preToken.Tokens.IsNullOrEmpty())
            {
                continue;
            }

            tokens.AddRange(TokenizePreToken(preToken.Tokens.Values.ToList()));
        }

        Console.WriteLine($"Обработано за: {stopwatch.Elapsed:mm\\:ss\\.ff}\n");

        stopwatch.Stop();

        return tokens;
    }

    // Альтернативный вариант с использованием errors='replace' (более элегантный)
    public string Decode(IEnumerable<int> tokenIds)
    {
        if (tokenIds == null)
            return string.Empty;

        // Собираем все байты из токенов
        var allBytes = new List<byte>();

        foreach (int tokenId in tokenIds)
        {
            var token = GetToken(tokenId);
            if (token != null)
            {
                allBytes.AddRange(token.Bytes);
            }
        }

        var encoding = (Encoding)Encoding.UTF8.Clone();
        encoding.DecoderFallback = new DecoderReplacementFallback("\uFFFF");

        return encoding.GetString(allBytes.ToArray());
    }

    public Token GetToken(int value)
    {
        foreach (var kvp in _vocab)
        {
            if (kvp.Value == value)
            {
                return kvp.Key;
            }
        }

        return null;
    }

    private List<int> TokenizePreToken(List<Token> tokens)
    {
        var currentTokens = new List<Token>(tokens);
        bool pairFound;

        do
        {
            pairFound = false;

            for (int i = 0; i < currentTokens.Count - 1; i++)
            {
                var pairToken = new Token(new TokenPair(currentTokens[i], currentTokens[i + 1]));

                if (_vocab.TryGetValue(pairToken, out var _))
                {
                    var newTokens = new List<Token>();

                    for (int j = 0; j < i; j++)
                    {
                        newTokens.Add(currentTokens[j]);
                    }

                    newTokens.Add(pairToken);

                    for (int j = i + 2; j < currentTokens.Count; j++)
                    {
                        newTokens.Add(currentTokens[j]);
                    }

                    currentTokens = newTokens;
                    pairFound = true;
                    break; 
                }
            }
        } while (pairFound); 

        var result = new List<int>();
        foreach (var token in currentTokens)
        {
            result.Add(_vocab[token]);
        }

        return result;
    }

    private TokenPair? GetFrequensedTokensPair()
    {
        if (_tokenPairsCount.IsEmpty)
            return null;

        TokenPair bestPair = default;
        int maxCount = 0;
        bool isFirst = true;

        foreach (var kvp in _tokenPairsCount)
        {
            if (isFirst)
            {
                bestPair = kvp.Key;
                maxCount = kvp.Value;
                isFirst = false;
                continue;
            }

            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                bestPair = kvp.Key;
            }
            else if (kvp.Value == maxCount)
            {
                // Лексикографическое сравнение пар токенов
                if (CompareTokenPairs(kvp.Key, bestPair) > 0)
                {
                    bestPair = kvp.Key;
                }
            }
        }

        return bestPair;
    }

    private int CompareTokenPairs(TokenPair pair1, TokenPair pair2)
    {
        // Сравниваем первые токены
        int firstComparison = CompareTokens(pair1.First, pair2.First);
        if (firstComparison != 0)
            return firstComparison;

        // Если первые токены равны, сравниваем вторые
        return CompareTokens(pair1.Second, pair2.Second);
    }

    private int CompareTokens(Token token1, Token token2)
    {
        if (ReferenceEquals(token1, token2))
            return 0;

        if (token1 is null)
            return -1;

        if (token2 is null)
            return 1;

        byte[] bytes1 = token1.Bytes;
        byte[] bytes2 = token2.Bytes;

        // Лексикографическое сравнение байтовых массивов
        int minLength = Math.Min(bytes1.Length, bytes2.Length);
        for (int i = 0; i < minLength; i++)
        {
            int comparison = bytes1[i].CompareTo(bytes2[i]);
            if (comparison != 0)
                return comparison;
        }

        // Если все байты до minLength равны, более короткий массив считается меньшим
        return bytes1.Length.CompareTo(bytes2.Length);
    }

    private bool MergePairs()
    {
        if (_tokenPairsCount.Count == 0)
        {
            return false;
        }

        var frequensedPair = GetFrequensedTokensPair();
        if (!frequensedPair.HasValue)
        {
            return false;
        }

        var needMergePretokens = _tokenPairsHashSet[frequensedPair.Value];
        foreach (var needMergePreToken in needMergePretokens)
        {
            var preTokenCount = _preTokens[needMergePreToken];

            var tokenPairs = needMergePreToken.MergePair(frequensedPair.Value);

            foreach (var needUpdatePair in tokenPairs.NeedUpdateCounterPairs)
            {
                if (_tokenPairsHashSet.TryGetValue(needUpdatePair, out var _))
                {
                    _tokenPairsHashSet[needUpdatePair].Remove(needMergePreToken);
                    _tokenPairsCount[needUpdatePair] -= preTokenCount;
                    if (_tokenPairsCount[needUpdatePair] == 0)
                    {
                        _tokenPairsCount.Remove(needUpdatePair, out var _);
                        _tokenPairsHashSet.Remove(needUpdatePair, out var _);
                    }
                }
            }

            foreach (var newPair in tokenPairs.NewPairs)
            {
                _tokenPairsHashSet.AddOrUpdate(
                   newPair,
                   k => new HashSet<PreToken>() { needMergePreToken },
                   (k, existingSet) =>
                   {
                       existingSet.Add(needMergePreToken);
                       return existingSet;
                   }
               );

                _tokenPairsCount.AddOrUpdate(newPair, preTokenCount, (k, v) => v + preTokenCount);
            }
        }

        if (!_tokenPairsHashSet.Remove(frequensedPair.Value, out var _))
        {
            throw new Exception();
        }

        if (!_tokenPairsCount.Remove(frequensedPair.Value, out var _))
        {
            throw new Exception();
        }

        _merges.Add(frequensedPair.Value);

        var vocabToken = new Token(frequensedPair.Value);
        _vocab.TryAdd(vocabToken, _vocab.Max(e => e.Value) + 1);

        return true;
    }

    private void ProcessChunks(ConcurrentDictionary<int, string> chunks, string tokenSeparator, int maxConcurrent = 12)
    {

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrent
        };

        var blocksCount = 0;
        /*
                foreach (var chunk in chunks)
                {
                    blocksCount += chunk.Value.Split(tokenSeparator).Count();

                    ProcessChunk(chunk, tokenSeparator);
                }
        */
        Parallel.ForEach(chunks, options, chunk =>
        {
            ProcessChunk(chunk, tokenSeparator);
        });
    }

    private List<string> GetChunkBlocks(KeyValuePair<int, string> chunk, string tokenSeparator)
    {
        // Вместо Split используем ReadOnlySpan и IndexOf вручную
        var text = chunk.Value.AsSpan();
        var separator = tokenSeparator.AsSpan();
        var chunkBlocks = new List<string>(); // или List<ReadOnlySpan<char>> если можно

        int start = 0;
        while (true)
        {
            int end = text.Slice(start).IndexOf(separator, StringComparison.Ordinal);
            if (end == -1)
            {
                // Последний блок
                if (start < text.Length)
                    chunkBlocks.Add(text.Slice(start).ToString());
                break;
            }

            end += start; // Абсолютная позиция

            if (end > start) // Пропускаем пустые блоки
                chunkBlocks.Add(text.Slice(start, end - start).ToString());

            start = end + separator.Length;
        }

        return chunkBlocks;
    }

    private void ProcessChunk(KeyValuePair<int, string> chunk, string tokenSeparator)
    {
        //Получаем блоки текста внутри чанка
        var chunkBlocks = GetChunkBlocks(chunk, tokenSeparator);

        var capacity = chunk.Value.Length / 10;
        var preTokens = new Dictionary<PreToken, int>();
        var tokenPairData = new Dictionary<TokenPair, PairData>(capacity * 2);

        //Проходимся по каждому блоку текста и претокенизируем его
        for (var i = 0; i < chunkBlocks.Count; i++)
        {
            foreach (ValueMatch match in _pattern.EnumerateMatches(chunkBlocks[i]))
            {
                var matchValue = chunkBlocks[i].AsSpan(match.Index, match.Length);
                var preToken = new PreToken(matchValue.ToString());

                if (preToken.Tokens.IsNullOrEmpty())
                {
                    continue;
                }

                // Пишем так:
                ref int preTokenCount = ref CollectionsMarshal.GetValueRefOrAddDefault(preTokens, preToken, out bool exists);
                preTokenCount += 1;

                // Если претокен состоит из одного токена, пропускаем
                var tokens = preToken.Tokens;
                if (tokens.Count <= 1) continue;

                // Обрабатываем все пары токенов в претокене
                ProcessTokenPairs(tokens.Select(e => e.Value).ToList(), preToken, preTokenCount, tokenPairData);
            }
        }

        // Мержим результаты
        BulkInsertPreTokens(preTokens);
        BulkInsertTokenPairData(tokenPairData);
    }

    // Вспомогательный метод для обработки пар токенов
    private void ProcessTokenPairs(
        IReadOnlyList<Token> tokens,
        PreToken preToken,
        int preTokenCount,
        Dictionary<TokenPair, PairData> tokenPairData)
    {
        // Используем for вместо foreach для списка токенов
        for (int i = 1; i < tokens.Count; i++)
        {
            var tokenPair = new TokenPair(tokens[i - 1], tokens[i]);

            ref PairData pairData = ref CollectionsMarshal.GetValueRefOrAddDefault(
                tokenPairData, tokenPair, out bool exists);

            if (!exists)
            {
                // Создаем новую запись
                pairData = new PairData(preToken);
                // Устанавливаем правильный счетчик на основе preTokenCount
                pairData.Count = preTokenCount;
            }
            else
            {
                // Обновляем существующую запись
                pairData.PreTokens.Add(preToken);
                pairData.Count++; // Увеличиваем на 1
            }
        }
    }

    private void BulkInsertPreTokens(Dictionary<PreToken, int> localPreTokens)
    {
        foreach (var kvp in localPreTokens)
        {
            _preTokens.AddOrUpdate(kvp.Key, kvp.Value, (key, old) => old + kvp.Value);
        }
    }

    private void BulkInsertTokenPairData(Dictionary<TokenPair, PairData> tokenPairData)
    {
        foreach (var kvp in tokenPairData)
        {
            _tokenPairsHashSet[kvp.Key] = kvp.Value.PreTokens;
            _tokenPairsCount[kvp.Key] = kvp.Value.Count;
        }
    }

    private void InitVocabularity(string[] specialTokens = null)
    {
        _vocab.Clear();

        int id = 0;

        // Добавляем все байты (0-255)
        for (int i = 0; i < 256; i++)
        {
            var bytes = new byte[] { (byte)i };
            var token = new Token(bytes);

            _vocab[token] = id;
            id++;
        }
    }
}