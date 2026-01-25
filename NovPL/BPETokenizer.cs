using BenchmarkDotNet.Attributes;
using NoNPL.Comparers;
using NoNPL.DataReaders;
using NoNPL.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        Console.WriteLine($"Обработано за: {stopwatch.Elapsed:mm\\:ss\\.ff}\n");

        stopwatch.Stop();
    }

    [Benchmark]
    public TokenPair? GetFrequensedTokensPair()
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
                if(_tokenPairsHashSet.TryGetValue(needUpdatePair, out var _))
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

    private void ProcessChunk(KeyValuePair<int, string> chunk, string tokenSeparator)
    {
        //Получаем блоки текста внутри чанка
        var chunkBlocks = chunk.Value.Split(tokenSeparator);

        // Инициализируем словари с примерным размером для уменьшения реаллокаций
        var estimatedTokenCount = chunkBlocks.Length * 10;
        var estimatedPairCount = chunkBlocks.Length * 50;

        var preTokens = new Dictionary<PreToken, int>(estimatedTokenCount);
        var tokenPairsHashSet = new Dictionary<TokenPair, HashSet<PreToken>>(estimatedPairCount);
        var tokenPairsCount = new Dictionary<TokenPair, int>(estimatedPairCount);

        //Проходимся по каждому блоку текста и претокенизируем его
        for (var i = 0; i < chunkBlocks.Length; i++)
        {
            //Проходимся по совпадениям и записиываем претокены в локальный словарь
            var matches = _pattern.Matches(chunkBlocks[i]);
            /*var matches = chunkBlocks[i].Split(" ");*/
            for (var j = 0; j < matches.Count(); j++)
            {
                var preToken = new PreToken(matches[j].Value);

                int preTokenCount;
                if (preTokens.TryGetValue(preToken, out int currentCount))
                {
                    preTokenCount = currentCount + 1;
                    preTokens[preToken] = preTokenCount;
                }
                else
                {
                    preTokenCount = 1;
                    preTokens.Add(preToken, preTokenCount);
                }

                if (preToken.Tokens.Count == 1)
                {
                    continue;
                }
                else
                {
                    //делаем маппинг пар байтов на претокены
                    for (var k = 1; k < preToken.Tokens.Count; k++)
                    {
                        var tokenPair = new TokenPair(preToken.Tokens[k - 1], preToken.Tokens[k]);

                        //добавляем в маппинг пар байтов на претокены
                        if (tokenPairsHashSet.TryGetValue(tokenPair, out var tokenPreTokens))
                        {
                            tokenPairsHashSet[tokenPair].Add(preToken);
                        }
                        else
                        {
                            tokenPairsHashSet.Add(tokenPair, new HashSet<PreToken> { preToken });
                        }

                        //добавляем в подсчет пар байтов
                        if (tokenPairsCount.TryGetValue(tokenPair, out int tokenCount))
                        {
                            tokenPairsCount[tokenPair] = tokenCount + 1;
                        }
                        else
                        {
                            tokenPairsCount.Add(tokenPair, preTokenCount);
                        }
                    }
                }
            }
        }

        //мержим в основные
        BulkInsertPreTokens(preTokens);
        BulkInsertTokensHashSet(tokenPairsHashSet, tokenPairsCount);
    }

    private void BulkInsertPreTokens(Dictionary<PreToken, int> localPreTokens)
    {
        foreach (var kvp in localPreTokens)
        {
            _preTokens.AddOrUpdate(kvp.Key, kvp.Value, (key, old) => old + kvp.Value);
        }
    }

    private void BulkInsertTokensHashSet(
        Dictionary<TokenPair, HashSet<PreToken>> localTokensHashSet,
        Dictionary<TokenPair, int> localTokenCount)
    {
        foreach (var kvp in localTokensHashSet)
        {
            _tokenPairsHashSet.AddOrUpdate(
                kvp.Key,
                k => kvp.Value,
                (k, existingSet) =>
                {
                    lock (existingSet)
                    {
                        existingSet.UnionWith(kvp.Value);
                        return existingSet;
                    }
                }
            );

            var tokenPairCount = localTokenCount[kvp.Key];
            _tokenPairsCount.AddOrUpdate(kvp.Key, tokenPairCount, (key, old) => old + tokenPairCount);
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